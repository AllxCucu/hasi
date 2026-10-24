using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Power;
using Hasi.Core.Screens;

namespace Hasi.Core;

/// <summary>
/// Owns the stack of screens and applies the rules every screen shares: any key interrupts speech,
/// Escape goes back, F1/F2/F3 work everywhere. Single-threaded: the host calls it from one thread.
/// </summary>
public sealed class Shell : IShellServices, IScreenContext
{
    private readonly List<IScreen> _stack = [];
    private readonly Announcer _announcer;
    private readonly ISystemActions _system;
    private readonly IDiagnosticLog _log;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly PowerWatch _power = new();
    private bool _focusLostWhilePowerPending;
    private DateTimeOffset? _lastAnnouncedHour;
    private readonly BackgroundJobs _jobs;

    /// <param name="loop">
    /// The host's event loop, for background work (<see cref="RunInBackground"/>). Without one, posted actions run at once,
    /// delayed ones never, and background work runs on the calling thread.
    /// </param>
    public Shell(Announcer announcer, ILocalizer localizer, ShellOptions options, ISystemActions system, IDiagnosticLog log,
        IDiagnosticsService? diagnostics = null, TimeProvider? time = null, IEventLoop? loop = null)
    {
        _jobs = new BackgroundJobs(loop ?? new ImmediateEventLoop(), log, this);
        Time = time ?? TimeProvider.System;
        _diagnostics = diagnostics;
        _announcer = announcer;
        Localizer = localizer;
        Options = options;
        _system = system;
        _log = log;
    }

    public ILocalizer Localizer { get; }

    public ShellOptions Options { get; }

    public TimeProvider Time { get; }

    public IReadOnlyList<IScreen> Stack => _stack;

    public IScreen Current => _stack[^1];

    /// <summary>Raised after every change of the current screen.</summary>
    public event EventHandler? ScreenChanged;

    public void Start(IScreen root)
    {
        if (_stack.Count > 0) throw new InvalidOperationException("The shell is already started.");
        PushScreen(root);
        Utterance welcome = Localizer.Get("shell.welcome");
        Say(welcome.Then(root.Enter(EnterReason.Startup, this)));
    }

    /// <summary>
    /// True while a screen with private content (<see cref="ISensitiveScreen"/>) is open anywhere in the stack, or
    /// the key being handled began with one open: the diagnostic log then masks typed characters, everything said
    /// and that screen's title.
    /// </summary>
    public bool IsSensitive => _sensitiveTurn || _stack.Any(screen => screen is ISensitiveScreen);

    /// <summary>
    /// Set while the shell handles something that began with a private screen open, so that what is said after that
    /// screen closes (for example "Documentul Scrisoare a fost salvat.") is masked too.
    /// </summary>
    private bool _sensitiveTurn;

    public void Dispatch(InputEvent input)
    {
        if (_stack.Count == 0) throw new InvalidOperationException("The shell has not been started.");

        if (input.IsRepeat && !input.Command.RepeatsWhenHeld()) return;

        var sensitive = IsSensitive;
        if (input.Character is { } typed) _log.Write("CHAR", sensitive ? "'*'" : $"'{typed}'");
        else _log.Write("KEY", input.IsRepeat ? $"{input.Key} (repeat)" : input.Key.ToString());
        if (input.Command != Command.None) _log.Write("CMD", input.Command.ToString());

        var previous = _sensitiveTurn;
        _sensitiveTurn = sensitive;
        try
        {
            Handle(input);
        }
        finally
        {
            _sensitiveTurn = previous;
        }
    }

    private void Handle(InputEvent input)
    {
        // Any key interrupts speech. A modifier pressed alone does nothing else.
        _announcer.Silence();

        // "Învață tastele": every key, global ones included, is described by the screen instead of being executed.
        if (Current is ICapturesAllKeys)
        {
            Apply(Current.Handle(input, this));
            return;
        }

        if (input.Command == Command.StopSpeech
            || (input.Command == Command.None && input.Character is null && (input.Key.Key.IsModifier() || input.Key.Key.IsToggle())))
        {
            return;
        }

        switch (input.Command)
        {
            case Command.SayTime:
                Say(DateText.Time(Time.GetLocalNow(), Localizer));
                return;
            case Command.Help:
                Say(Current.Help(this));
                return;
            case Command.RepeatLast:
                _announcer.Repeat(sensitive: IsSensitive);
                return;
            case Command.WhereAmI:
                Say(Localizer.Get("shell.whereAmI", ("path", string.Join(", ", _stack.Select(s => s.Title)))));
                return;
            case Command.Exit when Current is ConfirmScreen or UnsavedWorkScreen:
                Say(Current.Enter(EnterReason.Initial, this));
                return;
            case Command.Exit:
                Apply(ConfirmExit());
                return;
            case Command.ToggleDiagnostic when _diagnostics is not null:
                var turnOn = !_diagnostics.IsOn;
                if (!turnOn) _log.Write("DIAG", "turned off");
                var said = DiagnosticsMessages.Switch(_diagnostics, Localizer, turnOn);
                if (turnOn) _log.Write("DIAG", $"turned on by {input.Key}");
                Say(said);
                return;
        }

        var result = Current.Handle(input, this);
        if (result is UnhandledResult && input.Command == Command.Back)
        {
            result = ScreenResult.Pop();
        }
        Apply(result);
    }

    /// <summary>
    /// A key was pressed whose meaning arrives later as a typed character (or never, like Caps Lock):
    /// stop speaking now, because any key interrupts speech.
    /// </summary>
    public void Interrupt(KeyChord key)
    {
        // That key names the character it types: masked like the character.
        _log.Write("KEY", IsSensitive ? "*" : key.ToString());
        _announcer.Silence();
    }

    /// <summary>True while the current screen is a text field.</summary>
    public bool IsTextEntryActive => _stack.Count > 0 && Current is ITextEntry;

    /// <summary>
    /// The key profile chords are resolved with: the current screen's own (a text editor), or the default one. A
    /// question or menu opened on top of an editor uses the default keys again.
    /// </summary>
    public KeyProfile ActiveKeyProfile => _stack.Count > 0 && Current is IHasKeyProfile screen ? screen.KeyProfile : KeyProfile.Default;

    /// <summary>
    /// Num Lock was switched. In a text field this changes what the keypad does, so it is announced;
    /// elsewhere the keypad always navigates and nothing is said.
    /// </summary>
    public void NumericKeypadModeChanged(bool numLockOn)
    {
        _log.Write("NUMPAD", numLockOn ? "digits" : "navigation");
        if (!IsTextEntryActive) return;
        Say(Localizer.Get(numLockOn ? "numpad.digits" : "numpad.navigation"));
    }

    /// <summary>Another window took the keyboard: say so, otherwise keys silently go elsewhere.</summary>
    public void FocusLost()
    {
        if (_stack.Count == 0) return;
        _log.Write("FOCUS", "lost");
        if (PowerPending)
        {
            // Windows' own screens take the keyboard while the session ends: not worth interrupting
            // "Calculatorul se repornește". Said with the outcome if the computer does not restart.
            _focusLostWhilePowerPending = true;
            return;
        }
        Say(FocusLostText);
    }

    /// <summary>The user asked HASI to come forward, but Windows kept another window in front.</summary>
    public void CannotComeForward()
    {
        _log.Write("FOCUS", "could not come forward");
        Say(Localizer.Get("shell.cannotComeForward"));
    }

    private string FocusLostText => Localizer.Get(Options.ReturnHotkey ? "shell.focusLost" : "shell.focusLost.altTab");

    /// <summary>HASI has the keyboard again: say where the user is.</summary>
    public void FocusRegained()
    {
        if (_stack.Count == 0) return;
        _log.Write("FOCUS", "regained");
        _focusLostWhilePowerPending = false;
        Say(Current.Enter(EnterReason.Return, this));
    }

    /// <summary>
    /// Called on the shell's thread every few seconds: first every open <see cref="ITickingScreen"/> gets the tick
    /// (to save a draft, for example), then the hourly announcement; late ticks never announce a stale hour.
    /// </summary>
    public void ClockTick()
    {
        if (_stack.Count == 0) return;
        var now = Time.GetLocalNow();
        // A copy: a tick may not change the stack, but a screen must never break the loop if it does.
        foreach (var screen in _stack.OfType<ITickingScreen>().ToList()) screen.Tick(now);

        if (!Options.HourlyChime) return;
        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        if (now - hour >= TimeSpan.FromMinutes(2) || _lastAnnouncedHour == hour) return;
        _lastAnnouncedHour = hour;
        Say(new Utterance(DateText.Time(hour, Localizer), UtterancePriority.Queue));
    }

    public ScreenResult GuardUnsaved(Func<ScreenResult> proceed) => GuardUnsaved(proceed, []);

    /// <param name="settled">Work already saved or discarded during this question, never asked about twice.</param>
    private ScreenResult GuardUnsaved(Func<ScreenResult> proceed, IReadOnlyList<IUnsavedWork> settled)
    {
        ArgumentNullException.ThrowIfNull(proceed);
        // The screen on top first: it is the one the user was working in.
        var work = _stack.AsEnumerable().Reverse().OfType<IUnsavedWork>()
            .FirstOrDefault(w => w.HasUnsavedChanges && !settled.Contains(w));
        if (work is null) return proceed();
        return ScreenResult.Push(new UnsavedWorkScreen(work, () => GuardUnsaved(proceed, [.. settled, work]), Localizer));
    }

    /// <summary>Tracked messages not reported yet: announcement id → the screen that asked and its token.</summary>
    private readonly Dictionary<long, (IScreen Owner, long Token)> _tracked = [];

    /// <summary>
    /// The speech channel reports the end of announcement <paramref name="announcementId"/>. Call it on the shell's
    /// thread, posted (never from inside an announcement): the report reaches the screen that asked, only while it is
    /// still open. Unknown ids (untracked messages, or reported already) are ignored.
    /// </summary>
    public void SpeechFinished(long announcementId, SpeechOutcome outcome)
    {
        if (!_tracked.Remove(announcementId, out var tracked)) return;
        if (tracked.Owner is not ISpeechListener listener || !_stack.Contains(tracked.Owner)) return;
        var result = listener.SpeechFinished(tracked.Token, outcome, this);
        // No key was pressed, so "this key has no function" would make no sense.
        if (result is not UnhandledResult) Apply(result);
    }

    public BackgroundJob RunInBackground<T>(IScreen owner, Func<CancellationToken, T> work, Func<T, IScreenContext, ScreenResult> onDone,
        Utterance? slowNotice = null) => _jobs.Run(owner, work, onDone, slowNotice);

    public ScreenResult ConfirmExit() => GuardUnsaved(() => ScreenResult.Confirm(
        Localizer.Get("shell.exit.confirm"),
        () =>
        {
            _system.RequestExit();
            return ScreenResult.Say(Localizer.Get("shell.exit.goodbye"));
        }));

    public ScreenResult ConfirmRestart() => GuardUnsaved(() => ConfirmPower(PowerAction.Restart));

    public ScreenResult ConfirmShutdown() => GuardUnsaved(() => ConfirmPower(PowerAction.Shutdown));

    /// <summary>A restart or shutdown was asked for and its outcome is not known yet.</summary>
    public bool PowerPending => _power.Active is not null;

    /// <summary>Windows accepted the restart or shutdown; HASI stays open until the session really ends.</summary>
    public void PowerStarted(PowerAction action, DateTimeOffset now)
    {
        _log.Write("POWER", $"{action} started");
        _power.Started(action, now);
    }

    /// <summary>Windows refused at once (for example a policy forbids it).</summary>
    public void PowerFailed(PowerAction action)
    {
        _log.Write("POWER", $"{action} refused");
        SayPowerOutcome(new Utterance(Localizer.Get(PowerKey(action, "failed")), Earcon: Earcon.Error));
    }

    /// <summary>Windows has begun closing the session.</summary>
    /// <remarks>
    /// Whoever ends the session, HASI never blocks it: each open screen with unsaved work keeps a draft at once,
    /// without asking. Called again while Windows waits, so the draft follows what is typed meanwhile.
    /// </remarks>
    public void PowerSessionEnding()
    {
        if (_power.SessionEnding()) _log.Write("POWER", "session ending");
        foreach (var work in _stack.OfType<IUnsavedWork>().ToList())
        {
            try
            {
                if (work.HasUnsavedChanges) work.SaveDraftNow();
            }
#pragma warning disable CA1031 // The session is ending: one failed draft must not stop the others or block Windows.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Only the type: the message may name a private document.
                _log.Write("ERROR", $"draft not saved: {ex.GetType().Name}");
            }
        }
    }

    /// <summary>Windows reported that the session goes on (the restart or shutdown was cancelled).</summary>
    public void PowerCancelled()
    {
        if (_power.Active is not { } action) return;
        _power.Cancelled();
        _log.Write("POWER", $"{action} cancelled");
        SayPowerOutcome(new Utterance(PowerSentence(action, "cancelled", "power.remainOpen"), Earcon: Earcon.Error));
    }

    /// <summary>Called about once a second while <see cref="PowerPending"/>.</summary>
    public void PowerTick(DateTimeOffset now)
    {
        if (_power.Active is not { } action) return;
        switch (_power.Tick(now))
        {
            case PowerStatus.NotStarted:
                _log.Write("POWER", $"{action} not started");
                SayPowerOutcome(new Utterance(Localizer.Get(PowerKey(action, "notStarted")), Earcon: Earcon.Error));
                break;
            case PowerStatus.Waiting:
                _log.Write("POWER", $"{action} waiting");
                Say(new Utterance(PowerSentence(action, "waiting", "power.openPrograms"), Earcon: Earcon.Error));
                break;
        }
    }

    private ScreenResult ConfirmPower(PowerAction action)
    {
        if (_power.Active is { } pending) return ScreenResult.Say(Localizer.Get(PowerKey(pending, "alreadyPending")));
        var simulated = _system.PowerIsSimulated;
        List<string> question = [Localizer.Get(PowerKey(action, "confirm"))];
        if (simulated) question.Add(Localizer.Get(PowerKey(action, "testMode")));
        if (ProgramsSentence("power.openPrograms") is { } programs)
        {
            question.Add(programs);
            question.Add(Localizer.Get("power.unsavedMayBeLost"));
        }
        return ScreenResult.Confirm(string.Join(" ", question), () =>
        {
            // A program blocked an earlier attempt and programs are still open: offer forcing, explicitly.
            if (!simulated && _power.WasBlocked && _system.OpenPrograms().Count > 0)
            {
                return ScreenResult.Confirm(Localizer.Get(PowerKey(action, "forceConfirm")), () => RequestPower(action, force: true));
            }
            return RequestPower(action, force: false);
        });

        ScreenResult RequestPower(PowerAction requested, bool force)
        {
            _system.RequestPower(requested, force);
            return ScreenResult.Say(Localizer.Get(simulated ? "power.simulatedGoodbye" : PowerKey(requested, "goodbye")));
        }
    }

    /// <summary>"Repornirea a fost anulată." plus which programs are open, if any.</summary>
    private string PowerSentence(PowerAction action, string what, string programsKey) =>
        ProgramsSentence(programsKey) is { } programs
            ? $"{Localizer.Get(PowerKey(action, what))} {programs}"
            : Localizer.Get(PowerKey(action, what));

    private string? ProgramsSentence(string key)
    {
        var programs = _system.OpenPrograms();
        if (programs.Count == 0) return null;
        var names = string.Join(", ", programs.Select(p =>
            p.Windows > 1 ? Localizer.Plural("power.programWindows", p.Windows, ("name", p.Name)) : p.Name));
        return Localizer.Plural(key, programs.Count, ("names", names));
    }

    /// <summary>The outcome ends the wait; if Windows took the keyboard meanwhile, say how to come back.</summary>
    private void SayPowerOutcome(Utterance outcome)
    {
        if (_focusLostWhilePowerPending)
        {
            _focusLostWhilePowerPending = false;
            outcome = outcome.Then(FocusLostText);
        }
        Say(outcome);
    }

    private static string PowerKey(PowerAction action, string what) =>
        $"power.{(action == PowerAction.Restart ? "restart" : "shutdown")}.{what}";

    /// <summary>
    /// The window is being closed from outside HASI's keys (taskbar, Alt+F4 handled by Windows): ask, even on
    /// screens that capture every key.
    /// </summary>
    public void RequestExitConfirmation()
    {
        _announcer.Silence();
        if (Current is ConfirmScreen or UnsavedWorkScreen) Say(Current.Enter(EnterReason.Initial, this));
        else Apply(ConfirmExit());
    }

    /// <summary>
    /// Applies a result that arrives later, not as the answer to a key (a check for updates finished, a download
    /// made progress). Must be called on the shell's thread. With <see cref="UtterancePriority.Queue"/> what it says
    /// waits for the current speech, so it does not cut the welcome or another message.
    /// </summary>
    public void Show(ScreenResult result, UtterancePriority priority = UtterancePriority.Interrupt)
    {
        if (_stack.Count == 0) return;
        var previous = (_priority, _sensitiveTurn);
        _priority = priority;
        _sensitiveTurn = IsSensitive;
        try
        {
            Apply(result);
        }
        finally
        {
            (_priority, _sensitiveTurn) = previous;
        }
    }

    /// <summary>Priority forced by <see cref="Show"/>, or null for each utterance's own.</summary>
    private UtterancePriority? _priority;

    /// <summary>Everything the shell says goes through here, so that private content never reaches the log.</summary>
    /// <param name="queued">Waits for the current speech whatever <see cref="Show"/> forces (the rest of a tracked series).</param>
    /// <returns>The announcement's id.</returns>
    private long Say(Utterance utterance, bool queued = false)
    {
        if (queued) utterance = utterance with { Priority = UtterancePriority.Queue };
        else if (_priority is { } forced) utterance = utterance with { Priority = forced };
        if (IsSensitive) utterance = utterance with { Sensitive = true };
        return _announcer.Say(utterance);
    }

    private void Apply(ScreenResult result)
    {
        switch (result)
        {
            case HandledResult:
                break;
            case SayResult say:
                Say(say.Utterance);
                break;
            case PushResult push:
                PushScreen(push.Screen);
                Say(push.Screen.Enter(EnterReason.Initial, this));
                break;
            case ReplaceResult replace:
                _stack.RemoveAt(_stack.Count - 1);
                PushScreen(replace.Screen);
                Say(replace.Screen.Enter(EnterReason.Initial, this));
                break;
            case PopResult pop:
                ApplyPop(pop);
                break;
            case ConfirmResult confirm:
                Apply(ScreenResult.Push(new ConfirmScreen(confirm.Question, confirm.OnYes, Localizer.Get("confirm.title"), confirm.Cancelled)));
                break;
            case UnhandledResult:
                Say(new Utterance(Localizer.Get("shell.keyUnavailable"), Earcon: Earcon.Unavailable));
                break;
            case SayTrackedResult tracked:
                // The owner is the screen current now; a screen that is not a listener gets the messages said only.
                var owner = Current is ISpeechListener ? Current : null;
                for (var i = 0; i < tracked.Items.Count; i++)
                {
                    var item = tracked.Items[i];
                    var id = Say(item.Utterance, queued: i > 0);
                    if (owner is not null) _tracked[id] = (owner, item.Token);
                }
                break;
            case PopToResult popTo:
                var target = _stack.LastIndexOf(popTo.Screen);
                if (target < 0)
                {
                    if (popTo.Then is not null) Apply(popTo.Then);
                    break;
                }
                if (target < _stack.Count - 1)
                {
                    _stack.RemoveRange(target + 1, _stack.Count - target - 1);
                    OnScreenChanged();
                }
                if (popTo.Then is not null) Apply(popTo.Then);
                else
                {
                    var entry = Current.Enter(EnterReason.Return, this);
                    Say(popTo.Prefix is null ? entry : popTo.Prefix.Then(entry));
                }
                break;
            case CloseResult close:
                var index = _stack.LastIndexOf(close.Screen);
                if (index <= 0) break; // Already closed; the main menu is never closed.
                _stack.RemoveRange(index, _stack.Count - index);
                OnScreenChanged();
                Apply(close.Then());
                break;
            default:
                throw new NotSupportedException($"Unknown screen result {result.GetType().Name}.");
        }
    }

    private void ApplyPop(PopResult pop)
    {
        if (_stack.Count == 1)
        {
            Say(Localizer.Get("shell.atRoot"));
            return;
        }

        _stack.RemoveAt(_stack.Count - 1);
        OnScreenChanged();

        if (pop.Then is not null)
        {
            Apply(pop.Then);
        }
        else if (pop.Announce)
        {
            var entry = Current.Enter(EnterReason.Return, this);
            Say(pop.Prefix is null ? entry : pop.Prefix.Then(entry));
        }
    }

    private void PushScreen(IScreen screen)
    {
        _stack.Add(screen);
        OnScreenChanged();
    }

    private void OnScreenChanged()
    {
        // The title of a private screen names a document or a person.
        _log.Write("SCREEN", string.Join(" > ", _stack.Select(s => s is ISensitiveScreen ? "***" : s.Title)));
        _jobs.CancelLeft(_stack); // Leaving a screen stops the work it started.
        ScreenChanged?.Invoke(this, EventArgs.Empty);
    }
}
