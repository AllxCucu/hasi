using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Hasi.Core;
using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Speech;
using Hasi.Modules.About;
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
    private IDiagnosticLog _log = NullDiagnosticLog.Instance;
    private readonly KeyMap _keyMap = KeyMap.CreateDefault();
    private bool _exitRequested;
    private PowerAction _power;
    private bool _simulatePower;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) ErrorLog.Write(ex);
        };
        SessionEnding += (_, _) =>
        {
            if (_window is not null) _window.AllowClose = true;
        };

        var options = StartupOptions.Parse(e.Args);
        _simulatePower = options.SimulatePower;
        var version = ApplicationVersion();
        _log = CreateDiagnosticLog(options);
        _log.Write("START", $"HASI {version}, {Environment.OSVersion.VersionString}");

        var localizer = JsonLocalizer.LoadRomanian(AppPaths.Resources);
        var normalizer = new RomanianNormalizer(
            AcronymLexicon.Load(AcronymLexicon.DefaultPath),
            NounGenderLexicon.LoadDefault(),
            word => _log.Write("LEXICON?", word));
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
        _shell = new Shell(_announcer, localizer, shellOptions, this, _log);
        var settings = new AppSettingsService(AppPaths.SettingsFile, savedSettings, options, speech, engine, shellOptions, _window, _log);
        settings.SaveFailed += (_, _) => _announcer.Say(new Utterance(localizer.Get("settings.saveProblem"), UtterancePriority.Queue, Earcon.Error));

        // Main menu order. Keep in sync with tests/Hasi.Transcripts.Tests/TranscriptTests.cs.
        IModule[] modules = [new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule()];
        var context = new ModuleContext(localizer, _shell, new ApplicationInfo(version), normalizer, comparison, settings);

        _shell.ScreenChanged += (_, _) => _window.ShowTitle(_shell.Current.Title);
        _window.ProducesText = chord => TextRouting.ProducesText(chord, _keyMap, _shell.IsTextEntryActive, ShellWindow.NumLockOn);
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
        var focusWasLost = false;
        _window.Deactivated += (_, _) =>
        {
            if (_exiting) return;
            focusWasLost = true;
            _shell.FocusLost();
        };
        _window.Activated += (_, _) =>
        {
            // The first activation happens at startup, while the welcome message is being said.
            if (!focusWasLost || _exiting) return;
            focusWasLost = false;
            _shell.FocusRegained();
        };
        _window.Show();
        _window.Activate();

        _shell.Start(MainMenu.Create(modules, context));
        if (voiceNotice is not null)
        {
            var earcon = engine.CurrentVoiceName is null ? Earcon.Error : Earcon.None;
            _announcer.Say(new Utterance(voiceNotice, UtterancePriority.Queue, earcon));
        }
        if (settingsProblem is not null)
        {
            _announcer.Say(new Utterance(localizer.Get("settings.loadProblem"), UtterancePriority.Queue, Earcon.Error));
        }

        // Windows may refuse to give the new window the keyboard (for example when started from a
        // background process). Without this check the user's keys would silently go elsewhere.
        var focusCheck = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        focusCheck.Tick += (_, _) =>
        {
            focusCheck.Stop();
            if (_window.HasKeyboard || focusWasLost || _exiting) return;
            focusWasLost = true;
            _shell.FocusLost();
        };
        focusCheck.Start();
    }

    public void RequestExit() => _exitRequested = true;

    public void RequestRestart()
    {
        _power = PowerAction.Restart;
        _exitRequested = true;
    }

    public void RequestShutdown()
    {
        _power = PowerAction.Shutdown;
        _exitRequested = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Dispose();
        foreach (var disposable in _disposables) disposable.Dispose();
        (_log as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnKey(KeyChord chord, bool isRepeat = false)
    {
        if (_exiting || _shell is null) return;
        _shell.Dispatch(new InputEvent(_keyMap.Resolve(chord), chord, IsRepeat: isRepeat));
        if (_exitRequested) _ = ExitAfterSpeechAsync();
    }

    private void OnCharacter(char c)
    {
        if (_exiting || _shell is null) return;
        _shell.Dispatch(new InputEvent(Command.None, new KeyChord(Key.Other), c));
        if (_exitRequested) _ = ExitAfterSpeechAsync();
    }

    /// <summary>
    /// Lets "La revedere" finish, then closes (and restarts or shuts down the computer if that was confirmed).
    /// Closes even if waiting fails. If the computer refuses to restart or shut down, HASI says so and stays open.
    /// </summary>
    private async Task ExitAfterSpeechAsync()
    {
        _exiting = true;
        _exitRequested = false;
        var power = _power;
        _power = PowerAction.None;
        try
        {
            _log.Write("EXIT", power == PowerAction.None ? "user confirmed" : $"user confirmed {power}");
            await _engine!.WaitUntilIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Nothing may stop a confirmed exit.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
        }

        if (power != PowerAction.None && !PowerActions.Run(power, _simulatePower, _log))
        {
            _exiting = false;
            _announcer?.Say(new Utterance(_shell!.Localizer.Get("shell.powerFailed"), Earcon: Earcon.Error));
            return;
        }
        _window!.AllowClose = true;
        Shutdown();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write(e.Exception);
        _log.Write("ERROR", $"{e.Exception.GetType().Name}: {e.Exception.Message}");
        if (_announcer is not null && _shell is not null)
        {
            e.Handled = true;
            _announcer.Say(new Utterance(_shell.Localizer.Get("shell.unexpectedError"), Earcon: Earcon.Error));
        }
    }

    private IDiagnosticLog CreateDiagnosticLog(StartupOptions options)
    {
        if (!options.Diagnostic) return NullDiagnosticLog.Instance;
        try
        {
            var path = AppPaths.NewDiagnosticSessionFile(DateTimeOffset.Now);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new TextDiagnosticLog(new StreamWriter(path, append: false, new UTF8Encoding(true)), TimeProvider.System);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex);
            return NullDiagnosticLog.Instance;
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
