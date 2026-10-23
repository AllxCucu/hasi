using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
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

    public Shell(Announcer announcer, ILocalizer localizer, ShellOptions options, ISystemActions system, IDiagnosticLog log)
    {
        _announcer = announcer;
        Localizer = localizer;
        Options = options;
        _system = system;
        _log = log;
    }

    public ILocalizer Localizer { get; }

    public ShellOptions Options { get; }

    public IReadOnlyList<IScreen> Stack => _stack;

    public IScreen Current => _stack[^1];

    /// <summary>Raised after every change of the current screen.</summary>
    public event EventHandler? ScreenChanged;

    public void Start(IScreen root)
    {
        if (_stack.Count > 0) throw new InvalidOperationException("The shell is already started.");
        PushScreen(root);
        Utterance welcome = Localizer.Get("shell.welcome");
        _announcer.Say(welcome.Then(root.Enter(EnterReason.Startup, this)));
    }

    public void Dispatch(InputEvent input)
    {
        if (_stack.Count == 0) throw new InvalidOperationException("The shell has not been started.");

        if (input.IsRepeat && !input.Command.RepeatsWhenHeld()) return;

        if (input.Character is { } typed) _log.Write("CHAR", $"'{typed}'");
        else _log.Write("KEY", input.IsRepeat ? $"{input.Key} (repeat)" : input.Key.ToString());
        if (input.Command != Command.None) _log.Write("CMD", input.Command.ToString());

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
            case Command.Help:
                _announcer.Say(Current.Help(this));
                return;
            case Command.RepeatLast:
                _announcer.Repeat();
                return;
            case Command.WhereAmI:
                _announcer.Say(Localizer.Get("shell.whereAmI", ("path", string.Join(", ", _stack.Select(s => s.Title)))));
                return;
            case Command.Exit when Current is ConfirmScreen:
                _announcer.Say(Current.Enter(EnterReason.Initial, this));
                return;
            case Command.Exit:
                Apply(ConfirmExit());
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
        _log.Write("KEY", key.ToString());
        _announcer.Silence();
    }

    /// <summary>True while the current screen is a text field.</summary>
    public bool IsTextEntryActive => _stack.Count > 0 && Current is ITextEntry;

    /// <summary>
    /// Num Lock was switched. In a text field this changes what the keypad does, so it is announced;
    /// elsewhere the keypad always navigates and nothing is said.
    /// </summary>
    public void NumericKeypadModeChanged(bool numLockOn)
    {
        _log.Write("NUMPAD", numLockOn ? "digits" : "navigation");
        if (!IsTextEntryActive) return;
        _announcer.Say(Localizer.Get(numLockOn ? "numpad.digits" : "numpad.navigation"));
    }

    /// <summary>Another window took the keyboard: say so, otherwise keys silently go elsewhere.</summary>
    public void FocusLost()
    {
        if (_stack.Count == 0) return;
        _log.Write("FOCUS", "lost");
        _announcer.Say(Localizer.Get("shell.focusLost"));
    }

    /// <summary>HASI has the keyboard again: say where the user is.</summary>
    public void FocusRegained()
    {
        if (_stack.Count == 0) return;
        _log.Write("FOCUS", "regained");
        _announcer.Say(Current.Enter(EnterReason.Return, this));
    }

    public ScreenResult ConfirmExit() => ScreenResult.Confirm(
        Localizer.Get("shell.exit.confirm"),
        () =>
        {
            _system.RequestExit();
            return ScreenResult.Say(Localizer.Get("shell.exit.goodbye"));
        });

    public ScreenResult ConfirmRestart() => ScreenResult.Confirm(
        Localizer.Get("shell.restart.confirm"),
        () =>
        {
            _system.RequestRestart();
            return ScreenResult.Say(Localizer.Get("shell.restart.goodbye"));
        });

    public ScreenResult ConfirmShutdown() => ScreenResult.Confirm(
        Localizer.Get("shell.shutdown.confirm"),
        () =>
        {
            _system.RequestShutdown();
            return ScreenResult.Say(Localizer.Get("shell.shutdown.goodbye"));
        });

    /// <summary>
    /// The window is being closed from outside HASI's keys (taskbar, Alt+F4 handled by Windows): ask, even on
    /// screens that capture every key.
    /// </summary>
    public void RequestExitConfirmation()
    {
        _announcer.Silence();
        if (Current is ConfirmScreen) _announcer.Say(Current.Enter(EnterReason.Initial, this));
        else Apply(ConfirmExit());
    }

    private void Apply(ScreenResult result)
    {
        switch (result)
        {
            case HandledResult:
                break;
            case SayResult say:
                _announcer.Say(say.Utterance);
                break;
            case PushResult push:
                PushScreen(push.Screen);
                _announcer.Say(push.Screen.Enter(EnterReason.Initial, this));
                break;
            case ReplaceResult replace:
                _stack.RemoveAt(_stack.Count - 1);
                PushScreen(replace.Screen);
                _announcer.Say(replace.Screen.Enter(EnterReason.Initial, this));
                break;
            case PopResult pop:
                ApplyPop(pop);
                break;
            case ConfirmResult confirm:
                Apply(ScreenResult.Push(new ConfirmScreen(confirm.Question, confirm.OnYes, Localizer.Get("confirm.title"))));
                break;
            case UnhandledResult:
                _announcer.Say(new Utterance(Localizer.Get("shell.keyUnavailable"), Earcon: Earcon.Unavailable));
                break;
            default:
                throw new NotSupportedException($"Unknown screen result {result.GetType().Name}.");
        }
    }

    private void ApplyPop(PopResult pop)
    {
        if (_stack.Count == 1)
        {
            _announcer.Say(Localizer.Get("shell.atRoot"));
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
            _announcer.Say(pop.Prefix is null ? entry : pop.Prefix.Then(entry));
        }
    }

    private void PushScreen(IScreen screen)
    {
        _stack.Add(screen);
        OnScreenChanged();
    }

    private void OnScreenChanged()
    {
        _log.Write("SCREEN", string.Join(" > ", _stack.Select(s => s.Title)));
        ScreenChanged?.Invoke(this, EventArgs.Empty);
    }
}
