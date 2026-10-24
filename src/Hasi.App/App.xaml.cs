using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Hasi.Core;
using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Power;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Speech;
using Hasi.Core.Storage;
using Hasi.Core.Updates;
using Hasi.Documents;
using Hasi.Modules.About;
using Hasi.Modules.Clock;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Speech;
using Hasi.Text.Ro;

namespace Hasi.App;

/// <summary>Composition root: the only place that knows every concrete piece.</summary>
public partial class App : Application, ISystemActions
{
    private readonly List<IDisposable> _disposables = [];
    private ShellWindow? _window;
    private Shell? _shell;
    private Announcer? _announcer;
    private ISpeechEngine? _engine;
    private readonly SwitchableDiagnosticLog _log = new();
    private readonly ErrorBurst _errorBurst = new();
    private AppDiagnostics? _diagnostics;
    private CrashRecovery? _recovery;
    private ILocalizer? _localizer;
    private SingleInstance? _instance;
    private bool _focusWasLost;
    private readonly KeyMap _keyMap = KeyMap.CreateDefault();
    private bool _exitRequested;
    private PowerAction? _power;
    private bool _forcePower;
    private StartupOptions _options = StartupOptions.Parse([]);
    private AppSettingsService? _settings;
    private DispatcherTimer? _powerTimer;
    private DispatcherTimer? _clockTimer;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var options = StartupOptions.Parse(e.Args);
        _options = options;
        var version = ApplicationVersion();
        var header = $"HASI {version}, {Environment.OSVersion.VersionString}";
        ErrorLog.Initialize(header);
        // After an error or an update the previous HASI may still be ending: wait for it instead of handing over.
        _instance = SingleInstance.TryAcquire(waitForPrevious: options.AfterError || Program.Events.UpdatedTo is not null);
        if (_instance is null)
        {
            // HASI already runs: bring that one forward instead of opening a second window.
            SingleInstance.AskRunningToComeForward();
            Shutdown();
            return;
        }
        _recovery = new CrashRecovery(e.Args, _log);
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            // The process is ending: restart it, so the user is not left in silence.
            ErrorLog.Write(args.ExceptionObject as Exception, "crash");
            _log.Write("ERROR", $"crash: {(args.ExceptionObject as Exception)?.Message}");
            RecoverFromSeriousError("crash");
        };
        SessionEnding += (_, _) =>
        {
            // HASI never blocks a restart or shutdown, whoever asked for it.
            if (_window is not null) _window.AllowClose = true;
            _shell?.PowerSessionEnding();
        };

        _diagnostics = new AppDiagnostics(_log, header, TimeProvider.System);
        _recovery.DiagnosticIsOn = () => _diagnostics.IsOn;
        if (options.Diagnostic) _diagnostics.TurnOn();
        if (options.AfterError) _log.Write("START", "restarted after a serious error");

        var localizer = JsonLocalizer.LoadRomanian(AppPaths.Resources);
        _localizer = localizer;
        var normalizer = new RomanianNormalizer(
            AcronymLexicon.Load(AcronymLexicon.DefaultPath),
            NounGenderLexicon.LoadDefault(),
            // A word from a private document is not written down.
            word => _log.Write("LEXICON?", _shell?.IsSensitive == true ? TextDiagnosticLog.Mask : word));
        var savedSettings = SettingsFile.Load(AppPaths.SettingsFile, out var settingsProblem);
        if (settingsProblem is not null) _log.Write("ERROR", $"settings file: {settingsProblem}");
        var effectiveSettings = AppSettingsService.Effective(savedSettings, options);
        var speech = new SpeechSetup(options, effectiveSettings, _log, localizer);
        var (engine, voiceNotice) = speech.CreateMainEngine(localizer);
        _engine = engine;
        _log.Write("ENGINE", $"{engine.Id} voice=\"{engine.CurrentVoiceName}\"");
        var comparison = speech.CreateComparison(localizer, normalizer);
        if (comparison is not null) _disposables.Add(comparison);

        _window = new ShellWindow();
        var earcons = new ToneEarconPlayer();
        _disposables.Add(earcons);
        List<IOutputChannel> channels = [new SpeechOutputChannel(engine, earcons, _log), new VisualChannel(_window)];
        if (comparison is not null) channels.Add(comparison);
        var hub = new OutputHub(channels, _log);
        engine.Failed += (_, ex) =>
        {
            // Every voice has failed. Never fail in silence: log it and play the error tone.
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"no voice left: {ex.GetType().Name}: {ex.Message}");
            earcons.Play(Earcon.Error);
            _announcer?.Say(new Utterance(localizer.Get("speech.noVoice"), Earcon: Earcon.Error));
        };
        engine.VoiceSwitched += (_, switched) =>
        {
            ErrorLog.Write(switched.Reason);
            _log.Write("ENGINE", $"switched from \"{switched.FailedVoice}\" to \"{switched.NewVoice}\": {switched.Reason.Message}");
            // Posted, because the failure may be reported in the middle of saying something: once that
            // message is recorded as the last one, tell the user and repeat it, so nothing is lost.
            Dispatcher.BeginInvoke(() =>
            {
                if (_announcer is null || _exiting) return;
                Utterance notice = localizer.Get("speech.switched", ("failed", switched.FailedVoice), ("voice", switched.NewVoice));
                _announcer.Say(_announcer.Last is { } last ? notice.Then(last) : notice);
            });
        };
        _announcer = new Announcer(hub, normalizer, _log);
        var shellOptions = new ShellOptions();
        _shell = new Shell(_announcer, localizer, shellOptions, this, _log, _diagnostics);
        var settings = new AppSettingsService(AppPaths.SettingsFile, savedSettings, options, speech, engine, shellOptions, _window, _log);
        _settings = settings;
        settings.SaveFailed += (_, _) => _announcer.Say(new Utterance(localizer.Get("settings.saveProblem"), UtterancePriority.Queue, Earcon.Error));

        // Main menu order. Keep in sync with tests/Hasi.Transcripts.Tests/TranscriptTests.cs.
        IModule[] modules = [new ClockModule(), new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule()];
        var autostart = new RegistryAutostart(_log);
        var updates = new UpdateFlow(CreateUpdates(options, settings, version), settings, _shell, localizer, _log);
        var context = new ModuleContext(localizer, _shell, new ApplicationInfo(version), normalizer, comparison, settings, _diagnostics,
            autostart, updates, _shell.Time, UserData: new JsonUserDataStore(AppPaths.UserData, TimeProvider.System),
            Documents: new DocumentFormats(), Files: new WindowsFileSystem());

        _shell.ScreenChanged += (_, _) => _window.ShowTitle(_shell.Current.Title);
        _window.ProducesText = chord => TextRouting.ProducesText(chord, _keyMap, _shell.IsTextEntryActive, ShellWindow.NumLockOn,
            _shell.ActiveKeyProfile);
        _window.NumLockChanged += (_, on) =>
        {
            if (!_exiting) _shell.NumericKeypadModeChanged(on);
        };
        _window.KeyPressed += (_, key) => OnKey(key.Chord, key.IsRepeat);
        _window.TextKeyPressed += (_, chord) =>
        {
            if (!_exiting) _shell.Interrupt(chord);
        };
        _window.CharacterTyped += (_, c) => OnCharacter(c);
        _window.CloseRequested += (_, _) =>
        {
            if (_exiting) return;
            _shell.RequestExitConfirmation();
        };

        _window.Deactivated += (_, _) =>
        {
            if (_exiting) return;
            _focusWasLost = true;
            _shell.FocusLost();
        };
        _window.Activated += (_, _) =>
        {
            // The first activation happens at startup, while the welcome message is being said.
            if (!_focusWasLost || _exiting) return;
            _focusWasLost = false;
            _shell.FocusRegained();
        };
        _window.Show();
        _window.Activate();
        HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle)?.AddHook(OnWindowMessage);
        shellOptions.ReturnHotkey = _window.RegisterReturnHotkey();
        _log.Write("FOCUS", shellOptions.ReturnHotkey ? "Ctrl+Alt+H registered" : "Ctrl+Alt+H taken by another program");
        _instance.Listen(() => Dispatcher.BeginInvoke(() => ComeForward("started again")));

        _shell.Start(MainMenu.Create(modules, context));
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _clockTimer.Tick += (_, _) =>
        {
            if (!_exiting) _shell.ClockTick();
        };
        _clockTimer.Start();
        if (voiceNotice is not null)
        {
            var earcon = engine.CurrentVoiceName is null ? Earcon.Error : Earcon.None;
            _announcer.Say(new Utterance(voiceNotice, UtterancePriority.Queue, earcon));
        }
        if (settingsProblem is not null)
        {
            _announcer.Say(new Utterance(localizer.Get("settings.loadProblem"), UtterancePriority.Queue, Earcon.Error));
        }
        // The first start after installing turns autostart on (the owner's decision), and says so once.
        var autostartTurnedOn = Program.Events.FirstRun && autostart.SetEnabled(true);
        var facts = new StartupFacts(
            AfterError: options.AfterError,
            NarratorRunning: NarratorIsRunning(),
            RealPowerNotice: settings.TakeRealPowerNotice(),
            ReturnHotkeyUnavailable: !shellOptions.ReturnHotkey,
            UpdatedTo: Program.Events.UpdatedTo,
            AutostartTurnedOn: autostartTurnedOn);
        _log.Write("START", facts.ToString());
        foreach (var notice in StartupNotices.For(facts, localizer)) _announcer.Say(notice);
        // Then what the modules have to tell (a document left unsaved by a serious error or a power cut).
        MainMenu.ShowStartupNotices(_shell, modules, context);
        // After an update the new version has just been checked for; asking again now would be noise.
        if (Program.Events.UpdatedTo is null) updates.CheckAtStartup();
        HangWatchdog.Start(Dispatcher, () => RecoverFromSeriousError("frozen"));
        if (options.TestError is { } test) TestErrors.Schedule(test, Dispatcher);

        // Windows may refuse to give the new window the keyboard (for example when started from a
        // background process). Without this check the user's keys would silently go elsewhere.
        var focusCheck = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        focusCheck.Tick += (_, _) =>
        {
            focusCheck.Stop();
            if (_window.HasKeyboard || _focusWasLost || _exiting) return;
            _focusWasLost = true;
            _shell.FocusLost();
        };
        focusCheck.Start();
    }

    public void RequestExit() => _exitRequested = true;

    /// <summary>
    /// Ctrl+Alt+H or a second start: HASI comes to the front and says where the user is. Windows may refuse
    /// (another program is in the middle of something): then HASI says how to come back by hand.
    /// </summary>
    private void ComeForward(string reason)
    {
        if (_window is null || _shell is null || _exiting) return;
        _log.Write("FOCUS", $"come forward ({reason})");
        if (_window.HasKeyboard)
        {
            _shell.FocusRegained();
            return;
        }
        _focusWasLost = true; // So that activation says where the user is.
        _window.BringToFront();
        var check = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        check.Tick += (_, _) =>
        {
            check.Stop();
            if (!_window.HasKeyboard && !_exiting) _shell.CannotComeForward();
        };
        check.Start();
    }

    private VelopackUpdates CreateUpdates(StartupOptions options, AppSettingsService settings, string version)
    {
        var text = options.UpdateSource ?? settings.Current.UpdateSource;
        UpdateSource source;
        try
        {
            source = UpdateSource.Parse(text);
        }
        catch (FormatException ex)
        {
            ErrorLog.Write(ex, "update source");
            source = UpdateSource.Default;
        }
        _log.Write("UPDATE", $"source {source.Kind} {source.Location}");
        return new VelopackUpdates(source, version, Dispatcher, _log,
            () => _engine!.WaitUntilIdleAsync(TimeSpan.FromSeconds(5)),
            () =>
            {
                _exiting = true;
                _window!.AllowClose = true;
            });
    }

    private static bool NarratorIsRunning()
    {
        var narrators = System.Diagnostics.Process.GetProcessesByName("Narrator");
        foreach (var process in narrators) process.Dispose();
        return narrators.Length > 0;
    }

    public void RequestPower(PowerAction action, bool force)
    {
        _power = action;
        _forcePower = force;
        _exitRequested = true;
    }

    /// <summary><c>--simulate-power</c> and <c>--real-power</c> win for the session; otherwise "Simulare oprire".</summary>
    public bool PowerIsSimulated =>
        _options.SimulatePower || (!_options.RealPower && (_settings?.Current.SimulatePower ?? true));

    public IReadOnlyList<OpenProgram> OpenPrograms() => OpenWindows.List(_log);

    protected override void OnExit(ExitEventArgs e)
    {
        _clockTimer?.Stop();
        _engine?.Dispose();
        foreach (var disposable in _disposables) disposable.Dispose();
        _diagnostics?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnKey(KeyChord chord, bool isRepeat = false)
    {
        if (_exiting || _shell is null) return;
        _shell.Dispatch(new InputEvent(_keyMap.Resolve(chord, _shell.ActiveKeyProfile), chord, IsRepeat: isRepeat));
        if (_exitRequested) _ = ExitAfterSpeechAsync();
    }

    private void OnCharacter(char c)
    {
        if (_exiting || _shell is null) return;
        _shell.Dispatch(new InputEvent(Command.None, new KeyChord(Key.Other), c));
        if (_exitRequested) _ = ExitAfterSpeechAsync();
    }

    /// <summary>
    /// Lets the goodbye finish, then closes HASI. For a real restart or shutdown HASI asks Windows and stays
    /// open: Windows closes it when the session really ends, and until then the shell follows what happens.
    /// </summary>
    private async Task ExitAfterSpeechAsync()
    {
        _exiting = true;
        _exitRequested = false;
        var power = _power;
        _power = null;
        try
        {
            _log.Write("EXIT", power is null ? "user confirmed" : $"user confirmed {power}{(_forcePower ? " (forced)" : "")}");
            await _engine!.WaitUntilIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Nothing may stop a confirmed exit.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
        }

        if (power is { } action)
        {
            var simulated = PowerIsSimulated;
            var accepted = PowerActions.Run(action, _forcePower, simulated, _log);
            if (!simulated)
            {
                _exiting = false;
                if (!accepted)
                {
                    _shell!.PowerFailed(action);
                    return;
                }
                _shell!.PowerStarted(action, DateTimeOffset.Now);
                StartPowerTimer();
                return;
            }
        }
        _window!.AllowClose = true;
        Shutdown();
    }

    /// <summary>Once a second while a restart or shutdown is pending, until it is resolved.</summary>
    private void StartPowerTimer()
    {
        if (_powerTimer is null)
        {
            _powerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _powerTimer.Tick += (_, _) =>
            {
                if (PowerActions.SessionIsEnding) _shell!.PowerSessionEnding();
                _shell!.PowerTick(DateTimeOffset.Now);
                if (!_shell.PowerPending) _powerTimer.Stop();
            };
        }
        _powerTimer.Start();
    }

    private const int WmEndSession = 0x0016;
    private const int WmHotkey = 0x0312;

    /// <summary>WPF reports the start of a session end (<see cref="Application.SessionEnding"/>) but not its cancellation.</summary>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam == ShellWindow.ReturnHotkeyId)
        {
            handled = true;
            ComeForward("Ctrl+Alt+H");
        }
        else if (message == WmEndSession && wParam == IntPtr.Zero)
        {
            // The session goes on: closing the window must ask again.
            _log.Write("POWER", "session end cancelled");
            if (_window is not null) _window.AllowClose = _exiting;
            _shell?.PowerCancelled();
        }
        return IntPtr.Zero;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write(e.Exception, "unhandled UI exception");
        _log.Write("ERROR", $"{e.Exception.GetType().Name}: {e.Exception.Message}");
        if (_announcer is null || _shell is null) return; // During startup: the crash handler restarts HASI.
        e.Handled = true;
        if (_errorBurst.Add(DateTimeOffset.Now))
        {
            // Something is broken for good. Off the window's thread, so the message can still be said.
            Task.Run(() => RecoverFromSeriousError("repeated errors"));
            return;
        }
        _announcer.Say(new Utterance(_shell.Localizer.Get("shell.unexpectedError"), Earcon: Earcon.Error));
    }

    /// <summary>Restarts HASI after a serious error, or stops with a spoken explanation if restarting does not help.</summary>
    private void RecoverFromSeriousError(string reason)
    {
        var restarting = _localizer?.Get("error.restarting") ?? "";
        var givingUp = _localizer?.Get("error.givingUp") ?? "";
        _recovery?.RestartOrGiveUp(reason, restarting, givingUp, SayWithHasi);
    }

    /// <summary>Says <paramref name="text"/> with HASI's own voice and waits for it, if the window still answers.</summary>
    private bool SayWithHasi(string text)
    {
        if (_announcer is not { } announcer || _engine is not { } engine || text.Length == 0 || Dispatcher.CheckAccess()) return false;
        try
        {
            var say = Dispatcher.InvokeAsync(() => announcer.Say(new Utterance(text, Earcon: Earcon.Error)));
            if (say.Wait(TimeSpan.FromSeconds(1)) != DispatcherOperationStatus.Completed) return false;
            return engine.WaitUntilIdleAsync(TimeSpan.FromSeconds(8)).Wait(TimeSpan.FromSeconds(9));
        }
#pragma warning disable CA1031 // The voice may be what is broken; the caller then uses Windows' voice.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private static string ApplicationVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return informational.Split('+')[0];
    }
}

/// <summary>Shows each message in large text. Secondary to speech, but always on.</summary>
internal sealed class VisualChannel(ShellWindow window) : IOutputChannel
{
    public void Present(Announcement announcement)
    {
        var utterance = announcement.Utterance;
        window.ShowMessage(utterance.DisplayText ?? utterance.Text);
    }

    public void Cancel() { }
}
