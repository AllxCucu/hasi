using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Settings;

/// <summary>
/// User preferences. The settings themselves are implemented in phase 0.6; the Diagnostic submenu
/// already holds the pronunciation test (phase 0.4).
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
            .Select(key => key == "settings.diagnostic"
                ? new MenuItem(l.Get(key), _ => ScreenResult.Push(DiagnosticMenu(context)))
                : NotYetAvailable(l.Get(key), context))
            .ToList();
        return new MenuScreen(l.Get(TitleKey), items, help: l.Get("settings.help"));
    }

    private static MenuScreen DiagnosticMenu(IModuleContext context)
    {
        var l = context.Localizer;
        return new MenuScreen(l.Get("settings.diagnostic"),
        [
            new MenuItem(l.Get("diagnostic.pronunciation"), _ => ScreenResult.Push(PronunciationTest(context))),
            NotYetAvailable(l.Get("diagnostic.mode"), context),
            NotYetAvailable(l.Get("diagnostic.copyLog"), context),
        ]);
    }

    /// <summary>
    /// The tester types a text and hears it exactly as HASI would say it. The screen shows both the
    /// written and the spoken form; the diagnostic log records both.
    /// </summary>
    private static TextInputScreen PronunciationTest(IModuleContext context)
    {
        var l = context.Localizer;
        return new TextInputScreen(
            l.Get("diagnostic.pronunciation"),
            l.Get("pronunciation.prompt"),
            (text, _) => ScreenResult.Say(new Utterance(
                text,
                DisplayText: l.Get("pronunciation.display",
                    ("written", text), ("spoken", context.Normalizer.Normalize(text))))));
    }

    private static MenuItem NotYetAvailable(string label, IModuleContext context) =>
        new(label, _ => ScreenResult.Push(new MessageScreen(label, context.Localizer.Get("common.notYetAvailable"))));
}
