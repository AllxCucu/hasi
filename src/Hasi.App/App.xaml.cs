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
        engine.Failed += (_, ex) =>
        {
            // Never fail in silence: log it and play the error tone so the user knows something is wrong.
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"{engine.Id}: {ex.GetType().Name}: {ex.Message}");
            earcons.Play(Earcon.Error);
        };
        var normalizer = new RomanianNormalizer();
        _announcer = new Announcer(hub, normalizer, _log);
        _shell = new Shell(_announcer, localizer, new ShellOptions(), this, _log);

        // Main menu order. Keep in sync with tests/Hasi.Transcripts.Tests/TranscriptTests.cs.
        IModule[] modules = [new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule()];
        var context = new ModuleContext(localizer, _shell, new ApplicationInfo(version), normalizer);

        _shell.ScreenChanged += (_, _) => _window.ShowTitle(_shell.Current.Title);
        _window.IsCommandKey = chord => _keyMap.Resolve(chord) != Command.None;
        _window.KeyPressed += (_, key) => OnKey(key.Chord, key.IsRepeat);
        _window.TextKeyPressed += (_, chord) =>
        {
            if (!_exiting) _shell.Interrupt(chord);
        };
        _window.CharacterTyped += (_, c) => OnCharacter(c);
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
        if (voiceNotice is not null)
        {
            var earcon = engine is NullSpeechEngine ? Earcon.Error : Earcon.None;
            _announcer.Say(new Utterance(voiceNotice, UtterancePriority.Queue, earcon));
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

    /// <summary>Lets "La revedere" finish, then closes. Closes even if waiting fails.</summary>
    private async Task ExitAfterSpeechAsync()
    {
        _exiting = true;
        try
        {
            _log.Write("EXIT", "user confirmed");
            await _engine!.WaitUntilIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Nothing may stop a confirmed exit.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
        }
        finally
        {
            _window!.AllowClose = true;
            Shutdown();
        }
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
        ISpeechEngine? engine = null;
        VoiceCandidate? voice = null;
        // A voice that is listed may still fail to start; then try the next best one.
        foreach (var candidate in VoiceChooser.Rank(voices, preferred))
        {
            engine = candidate.Engine switch
            {
                "sapi5" when sapi is not null => Attempt($"SAPI5 voice {candidate.Name}", () =>
                {
                    sapi.SelectVoice(candidate.Name);
                    if (options.Rate is { } rate) sapi.Rate = rate;
                    return sapi;
                }),
                "onecore" => Attempt($"OneCore voice {candidate.Name}", () =>
                {
                    var oneCore = new OneCoreEngine();
                    oneCore.SelectVoice(candidate.Name);
                    if (options.Rate is { } rate) oneCore.Rate = rate;
                    return oneCore;
                }),
                _ => null,
            };
            if (engine is not null)
            {
                voice = candidate;
                break;
            }
        }
        if (!ReferenceEquals(engine, sapi)) sapi?.Dispose();

        if (engine is null || voice is null)
        {
            _log.Write("ERROR", "no working voice; text is shown on screen only");
            return (new NullSpeechEngine(), localizer.Get("speech.noVoice"));
        }
        var notice = options.Voice is null && !VoiceChooser.IsPreferred(voice, preferred)
            ? localizer.Get("speech.voiceFallback", ("voice", voice.Name))
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
