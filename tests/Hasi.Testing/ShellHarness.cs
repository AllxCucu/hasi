using Hasi.Core;
using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Testing;

public sealed class FakeSystemActions : ISystemActions
{
    public bool ExitRequested { get; private set; }

    public void RequestExit() => ExitRequested = true;
}

/// <summary>
/// Runs the real shell with the real Romanian resources and no audio or window:
/// press keys, then check what would have been said.
/// </summary>
public sealed class ShellHarness
{
    public ShellHarness(ShellOptions? options = null)
    {
        Localizer = JsonLocalizer.LoadRomanian(Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json"));
        Output = new RecordingChannel();
        System = new FakeSystemActions();
        KeyMap = KeyMap.CreateDefault();
        var log = NullDiagnosticLog.Instance;
        Announcer = new Announcer(new OutputHub([Output], log), new PassThroughNormalizer(), log);
        Shell = new Shell(Announcer, Localizer, options ?? new ShellOptions(), System, log);
        ModuleContext = new ModuleContext(Localizer, Shell, new ApplicationInfo("0.1.0"));
    }

    public JsonLocalizer Localizer { get; }

    public RecordingChannel Output { get; }

    public FakeSystemActions System { get; }

    public KeyMap KeyMap { get; }

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
            Shell.Dispatch(new InputEvent(KeyMap.Resolve(key), key));
        }
        return [.. Output.Announcements.Skip(before).Select(a => a.Utterance.Text)];
    }
}
