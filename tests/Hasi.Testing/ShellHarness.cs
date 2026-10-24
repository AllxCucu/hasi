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

namespace Hasi.Testing;

public sealed class FakeSystemActions : ISystemActions
{
    public bool ExitRequested { get; private set; }

    public PowerAction? PowerRequested { get; private set; }

    public bool ForceRequested { get; private set; }

    /// <summary>Whether restart and shut down only pretend; the harness ties it to "Simulare oprire".</summary>
    public Func<bool> Simulated { get; set; } = () => false;

    public bool PowerIsSimulated => Simulated();

    /// <summary>What <see cref="OpenPrograms"/> returns; none by default.</summary>
    public List<OpenProgram> Programs { get; } = [];

    public void RequestExit() => ExitRequested = true;

    public void RequestPower(PowerAction action, bool force)
    {
        PowerRequested = action;
        ForceRequested = force;
    }

    public IReadOnlyList<OpenProgram> OpenPrograms() => [.. Programs];
}

/// <summary>Diagnostic mode in memory: records the switches and returns the copy result a test sets.</summary>
public sealed class FakeDiagnosticsService : IDiagnosticsService
{
    public bool IsOn { get; private set; }

    /// <summary>Simulates a log file that cannot be written.</summary>
    public bool FailToStart { get; set; }

    public LogCopyResult CopyResult { get; set; } = LogCopyResult.Copied;

    public int Copies { get; private set; }

    public bool TurnOn()
    {
        if (FailToStart) return false;
        IsOn = true;
        return true;
    }

    public void TurnOff() => IsOn = false;

    public LogCopyResult CopyToDesktop()
    {
        Copies++;
        return CopyResult;
    }
}

/// <summary>Autostart in memory.</summary>
public sealed class FakeAutostartService : IAutostartService
{
    public bool IsEnabled { get; private set; }

    /// <summary>Simulates Windows refusing the change.</summary>
    public bool Refuse { get; set; }

    public bool SetEnabled(bool enabled)
    {
        if (Refuse) return false;
        IsEnabled = enabled;
        return true;
    }
}

/// <summary>
/// Runs the real shell with the real Romanian resources and no audio or window:
/// press keys, then check what would have been said.
/// </summary>
public sealed class ShellHarness
{
    /// <param name="normalizer">Pass-through by default, so tests see the texts exactly as in ro.json.</param>
    public ShellHarness(ShellOptions? options = null, ITextNormalizer? normalizer = null, IVoiceComparison? comparison = null, ISettingsService? settings = null,
        TimeProvider? time = null, IDiagnosticLog? diagnosticLog = null)
    {
        Localizer = JsonLocalizer.LoadRomanian(Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json"));
        Output = new RecordingChannel();
        System = new FakeSystemActions();
        KeyMap = KeyMap.CreateDefault();
        Normalizer = normalizer ?? new PassThroughNormalizer();
        var log = diagnosticLog ?? NullDiagnosticLog.Instance;
        Announcer = new Announcer(new OutputHub([Output], log), Normalizer, log);
        Diagnostics = new FakeDiagnosticsService();
        Autostart = new FakeAutostartService();
        Files = new FakeFileSystem();
        Shell =new Shell(Announcer, Localizer, options ?? new ShellOptions(), System, log, Diagnostics, time);
        Settings = settings ?? new InMemorySettingsService();
        Shell.Options.HourlyChime = options?.HourlyChime ?? Settings.Current.HourlyChime;
        System.Simulated = () => Settings.Current.SimulatePower;
        if (Settings is InMemorySettingsService memory)
        {
            // As the application does: list ends and key echo follow the settings at once.
            memory.Changed += (_, s) =>
            {
                Shell.Options.WrapLists = s.WrapLists;
                Shell.Options.HourlyChime = s.HourlyChime;
                Shell.Options.KeyEcho = s.KeyEcho;
            };
        }
        ModuleContext = new ModuleContext(Localizer, Shell, new ApplicationInfo("0.1.0"), Normalizer, comparison, Settings, Diagnostics, Autostart, Time: Shell.Time,
            Files: Files);
    }

    public JsonLocalizer Localizer { get; }

    public RecordingChannel Output { get; }

    public FakeSystemActions System { get; }

    public KeyMap KeyMap { get; }

    public ITextNormalizer Normalizer { get; }

    public ISettingsService Settings { get; }

    public FakeDiagnosticsService Diagnostics { get; }

    /// <summary>The disk modules see through <see cref="ModuleContext"/>: empty until a test adds places, folders and files.</summary>
    public FakeFileSystem Files { get; }

    public FakeAutostartService Autostart { get; }

    public Announcer Announcer { get; }

    public Shell Shell { get; }

    public IModuleContext ModuleContext { get; }

    /// <summary>The text of the most recent announcement, or null when nothing was said.</summary>
    public string? LastSaid => Output.Announcements.Count == 0 ? null : Output.Announcements[^1].Utterance.Text;

    public Announcement? LastAnnouncement => Output.Announcements.Count == 0 ? null : Output.Announcements[^1];

    public ShellHarness StartWithModules(params IModule[] modules)
    {
        Shell.Start(MainMenu.Create(modules, ModuleContext));
        return this;
    }

    public ShellHarness Start(IScreen root)
    {
        Shell.Start(root);
        return this;
    }

    /// <summary>Presses keys one after the other and returns what was said, in order.</summary>
    public IReadOnlyList<string> Press(params KeyChord[] keys)
    {
        var before = Output.Announcements.Count;
        foreach (var key in keys)
        {
            Shell.Dispatch(new InputEvent(KeyMap.Resolve(key, Shell.ActiveKeyProfile), key));
        }
        return [.. Output.Announcements.Skip(before).Select(a => a.Utterance.Text)];
    }

    /// <summary>Types text character by character, as the keyboard would, and returns what was said.</summary>
    public IReadOnlyList<string> Type(string text)
    {
        var before = Output.Announcements.Count;
        foreach (var c in text)
        {
            Shell.Dispatch(new InputEvent(Command.None, new KeyChord(Key.Other), c));
        }
        return [.. Output.Announcements.Skip(before).Select(a => a.Utterance.Text)];
    }

    /// <summary>Holds a key down: one press followed by <paramref name="repeats"/> auto-repeats.</summary>
    public IReadOnlyList<string> Hold(KeyChord key, int repeats)
    {
        var before = Output.Announcements.Count;
        Shell.Dispatch(new InputEvent(KeyMap.Resolve(key, Shell.ActiveKeyProfile), key));
        for (var i = 0; i < repeats; i++)
        {
            Shell.Dispatch(new InputEvent(KeyMap.Resolve(key, Shell.ActiveKeyProfile), key, IsRepeat: true));
        }
        return [.. Output.Announcements.Skip(before).Select(a => a.Utterance.Text)];
    }
}
