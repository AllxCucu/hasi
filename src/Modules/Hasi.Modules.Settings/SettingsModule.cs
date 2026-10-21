using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Settings;

/// <summary>
/// User preferences. Phase 0.3 lists the settings so navigation can be tested by ear;
/// each one is implemented in phase 0.6.
/// </summary>
public sealed class SettingsModule : IModule
{
    private static readonly string[] SettingKeys =
    [
        "settings.voice",
        "settings.rate",
        "settings.volume",
        "settings.pitch",
        "settings.keyEcho",
        "settings.colors",
        "settings.textSize",
        "settings.listEnd",
        "settings.autostart",
        "settings.screenReader",
        "settings.diagnostic",
        "settings.updates",
    ];

    public string Id => "settings";

    public string TitleKey => "settings.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        var l = context.Localizer;
        var items = SettingKeys
            .Select(key => l.Get(key))
            .Select(label => new MenuItem(label, _ =>
                ScreenResult.Push(new MessageScreen(label, l.Get("common.notYetAvailable")))))
            .ToList();
        return new MenuScreen(l.Get(TitleKey), items, help: l.Get("settings.help"));
    }
}
