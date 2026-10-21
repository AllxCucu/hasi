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
using Hasi.Core.Speech;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Speech;

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
        var version = ApplicationVersion();
        _log = CreateDiagnosticLog(options);
        _log.Write("START", $"HASI {version}, {Environment.OSVersion.VersionString}");

        var localizer = JsonLocalizer.LoadRomanian(AppPaths.Resources);
        var (engine, voiceNotice) = CreateSpeechEngine(options, localizer);
        _engine = engine;
        _log.Write("ENGINE", $"{engine.Id} voice=\"{engine.CurrentVoiceName}\"");

        _window = new ShellWindow();
        var earcons = new ToneEarconPlayer();
        _disposables.Add(earcons);
        var hub = new OutputHub(
            [new SpeechOutputChannel(engine, earcons, _log), new VisualChannel(_window)],
            _log);
        _announcer = new Announcer(hub, new PassThroughNormalizer(), _log);
        _shell = new Shell(_announcer, localizer, new ShellOptions(), this, _log);

        // Main menu order. Keep in sync with tests/Hasi.Transcripts.Tests/TranscriptTests.cs.
        IModule[] modules = [new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule()];
        var context = new ModuleContext(localizer, _shell, new ApplicationInfo(version));

        _shell.ScreenChanged += (_, _) => _window.ShowTitle(_shell.Current.Title);
        _window.KeyPressed += (_, chord) => OnKey(chord);
        _window.CloseRequested += (_, _) => OnKey(new KeyChord(Key.F4, KeyModifiers.Alt));
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
        if (voiceNotice is not null) _announcer.Say(new Utterance(voiceNotice, UtterancePriority.Queue));

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

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Dispose();
        foreach (var disposable in _disposables) disposable.Dispose();
        (_log as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnKey(KeyChord chord)
    {
        if (_exiting || _shell is null) return;
        _shell.Dispatch(new InputEvent(_keyMap.Resolve(chord), chord));
        if (_exitRequested) _ = ExitAfterSpeechAsync();
    }

    private async Task ExitAfterSpeechAsync()
    {
        _exiting = true;
        _log.Write("EXIT", "user confirmed");
        await _engine!.WaitUntilIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
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
        var path = AppPaths.NewDiagnosticSessionFile(DateTimeOffset.Now);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new TextDiagnosticLog(new StreamWriter(path, append: false, new UTF8Encoding(true)), TimeProvider.System);
    }

    /// <summary>
    /// Paul (RHVoice through SAPI5) if installed; otherwise a Romanian voice from SAPI5 or OneCore
    /// (Microsoft Andrei); otherwise any voice. A fallback is said aloud.
    /// </summary>
    private (ISpeechEngine Engine, string? Notice) CreateSpeechEngine(StartupOptions options, ILocalizer localizer)
    {
        Sapi5Engine? sapi = Attempt("SAPI5", () => new Sapi5Engine());
        var voices = new List<VoiceCandidate>();
        voices.AddRange(Attempt("SAPI5 voices", () => sapi?.Voices) ?? []);
        voices.AddRange(Attempt("OneCore voices", () => OneCoreEngine.Voices) ?? []);
        _log.Write("VOICES", string.Join(", ", voices.Select(v => $"{v.Engine}:\"{v.Name}\" {v.Culture}")));

        var preferred = options.Voice ?? VoiceChooser.DefaultPreferredVoice;
        var voice = VoiceChooser.Choose(voices, preferred);
        ISpeechEngine? engine = voice?.Engine switch
        {
            "sapi5" => Attempt("SAPI5 voice", () =>
            {
                sapi!.SelectVoice(voice.Name);
                if (options.Rate is { } rate) sapi.Rate = rate;
                return sapi;
            }),
            "onecore" => Attempt("OneCore voice", () =>
            {
                var oneCore = new OneCoreEngine();
                oneCore.SelectVoice(voice.Name);
                if (options.Rate is { } rate) oneCore.Rate = rate;
                oneCore.Failed += (_, ex) =>
                {
                    ErrorLog.Write(ex);
                    _log.Write("ERROR", $"OneCore: {ex.Message}");
                };
                return oneCore;
            }),
            _ => null,
        };
        if (!ReferenceEquals(engine, sapi)) sapi?.Dispose();

        if (engine is null)
        {
            _log.Write("ERROR", "no working voice; text is shown on screen only");
            return (new NullSpeechEngine(), null);
        }
        var notice = options.Voice is null && !VoiceChooser.IsPreferred(voice!, preferred)
            ? localizer.Get("speech.voiceFallback", ("voice", voice!.Name))
            : null;
        return (engine, notice);
    }

    private T? Attempt<T>(string what, Func<T?> action) where T : class
    {
        try
        {
            return action();
        }
#pragma warning disable CA1031 // Without speech HASI still shows every message on screen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"{what} unavailable: {ex.Message}");
            return null;
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
