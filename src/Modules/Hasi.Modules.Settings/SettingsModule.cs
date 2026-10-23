using Hasi.Core.Diagnostics;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Updates;

namespace Hasi.Modules.Settings;

/// <summary>
/// User preferences (phase 0.6): voice, rate, volume, pitch, key echo, colours, text size and list ends,
/// saved at once and applied immediately; autostart (0.10); updates (0.11).
/// </summary>
public sealed class SettingsModule : IModule
{
    public string Id => "settings";

    public string TitleKey => "settings.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        var l = context.Localizer;
        var s = context.Settings;
        MenuItem Item(string key, Func<IScreen>? open) =>
            open is null || s is null ? NotYetAvailable(l.Get(key), context) : new MenuItem(l.Get(key), _ => ScreenResult.Push(open()));

        List<MenuItem> items =
        [
            Item("settings.voice", () => VoiceChoice(context, s!)),
            Item("settings.rate", () => Percent(l.Get("settings.rate"), () => s!.Current.Rate, v => s!.Update(u => u with { Rate = v }), l.Get("settings.rate.normal"))),
            Item("settings.volume", () => Volume(context, s!)),
            Item("settings.pitch", () => Percent(l.Get("settings.pitch"), () => s!.Current.Pitch, v => s!.Update(u => u with { Pitch = v }), l.Get("settings.pitch.normal"))),
            Item("settings.keyEcho", () => Choice(context, "settings.keyEcho", ["settings.keyEcho.character", "settings.keyEcho.word", "settings.keyEcho.none"],
                (int)s!.Current.KeyEcho, i => s.Update(u => u with { KeyEcho = (KeyEcho)i }))),
            Item("settings.colors", () => Choice(context, "settings.colors",
                ["settings.colors.yellowOnBlack", "settings.colors.whiteOnBlack", "settings.colors.blackOnWhite", "settings.colors.blackOnYellow"],
                (int)s!.Current.Colors, i => s.Update(u => u with { Colors = (ColorScheme)i }))),
            Item("settings.textSize", () => Choice(context, "settings.textSize",
                ["settings.textSize.small", "settings.textSize.medium", "settings.textSize.large", "settings.textSize.extraLarge", "settings.textSize.huge"],
                (int)s!.Current.TextSize, i => s.Update(u => u with { TextSize = (TextSize)i }))),
            Item("settings.listEnd", () => Choice(context, "settings.listEnd", ["settings.listEnd.stop", "settings.listEnd.wrap"],
                s!.Current.WrapLists ? 1 : 0, i => s.Update(u => u with { WrapLists = i == 1 }))),
            context.Autostart is { } autostart
                ? new MenuItem(l.Get("settings.autostart"), _ => ScreenResult.Push(Autostart(context, autostart)))
                : NotYetAvailable(l.Get("settings.autostart"), context),
            new MenuItem(l.Get("settings.diagnostic"), _ => ScreenResult.Push(DiagnosticMenu(context))),
            context.Updates is { } updates && s is not null
                ? new MenuItem(l.Get("settings.updates"), _ => ScreenResult.Push(Updates(context, updates, s)))
                : NotYetAvailable(l.Get("settings.updates"), context),
        ];
        return new MenuScreen(l.Get(TitleKey), items, help: l.Get("settings.help"));

        // Rate and pitch: -10..10 inside HASI, said as 0..100 with 50 = the voice's own value.
        ValueScreen Percent(string title, Func<int> get, Action<int> set, string atNormal) => new(
            title,
            () => 50 + get() * 5,
            v => set((v - 50) / 5),
            0, 100, 5,
            v => v == 50 ? atNormal : v.ToString(l.Culture));
    }

    /// <summary>Check now, automatic check at startup, and the installed version.</summary>
    private static MenuScreen Updates(IModuleContext context, UpdateFlow updates, ISettingsService settings)
    {
        var l = context.Localizer;
        var auto = l.Get("settings.updates.auto");
        string[] autoLabels = [l.Get("settings.updates.auto.on"), l.Get("settings.updates.auto.off")];
        var version = l.Get("settings.updates.version", ("version", updates.CurrentVersion));
        return new MenuScreen(l.Get("settings.updates"),
        [
            new MenuItem(l.Get("settings.updates.check"), _ => updates.CheckNow()),
            new MenuItem(auto, _ => ScreenResult.Push(new ChoiceScreen(auto, autoLabels, settings.Current.AutoCheckUpdates ? 0 : 1,
                (index, _) =>
                {
                    settings.Update(u => u with { AutoCheckUpdates = index == 0 });
                    return ScreenResult.Pop(prefix: l.Get("settings.choice.changed", ("title", auto), ("option", autoLabels[index])));
                }, l.Get("settings.choice.current")))),
            new MenuItem(version, _ => ScreenResult.Say(version)),
        ], help: l.Get("settings.updates.help"));
    }

    private static ChoiceScreen Autostart(IModuleContext context, IAutostartService autostart)
    {
        var l = context.Localizer;
        var title = l.Get("settings.autostart");
        string[] labels = [l.Get("settings.autostart.on"), l.Get("settings.autostart.off")];
        return new ChoiceScreen(title, labels, autostart.IsEnabled ? 0 : 1, (index, _) =>
            autostart.SetEnabled(index == 0)
                ? ScreenResult.Pop(prefix: l.Get("settings.choice.changed", ("title", title), ("option", labels[index])))
                : ScreenResult.Say(new Utterance(l.Get("settings.autostart.failed"), Earcon: Earcon.Error)),
            l.Get("settings.choice.current"));
    }

    private static ValueScreen Volume(IModuleContext context, ISettingsService settings)
    {
        var l = context.Localizer;
        return new ValueScreen(
            l.Get("settings.volume"),
            () => settings.Current.Volume,
            v => settings.Update(u => u with { Volume = v }),
            0, 100, 10,
            v => v == 100 ? l.Get("settings.volume.max") : v.ToString(l.Culture));
    }

    /// <summary>
    /// Romanian voices only (plus the current voice, if it is not Romanian), with "Arată toate vocile" at the end;
    /// that last item replaces the list with every voice found.
    /// </summary>
    private static ChoiceScreen VoiceChoice(IModuleContext context, ISettingsService settings, bool showAll = false)
    {
        var l = context.Localizer;
        var voices = settings.Voices
            .Where(v => showAll || v.Romanian || v.Label == settings.CurrentVoiceLabel)
            .ToList();
        var hidden = settings.Voices.Count - voices.Count;
        List<string> labels = [.. voices.Select(v => v.Label)];
        if (hidden > 0) labels.Add(l.Get("settings.voice.showAll"));
        var current = Math.Max(0, voices.FindIndex(v => v.Label == settings.CurrentVoiceLabel));
        return new ChoiceScreen(
            l.Get("settings.voice"),
            labels,
            current,
            (index, _) =>
            {
                if (index == voices.Count) return ScreenResult.Replace(VoiceChoice(context, settings, showAll: true));
                return settings.SelectVoice(voices[index])
                    ? ScreenResult.Pop(prefix: l.Get("settings.voice.changed", ("voice", voices[index].Label)))
                    : ScreenResult.Say(new Utterance(
                        l.Get("settings.voice.failed", ("voice", voices[index].Label), ("current", settings.CurrentVoiceLabel)),
                        Earcon: Earcon.Error));
            },
            l.Get("settings.choice.current"));
    }

    private static ChoiceScreen Choice(IModuleContext context, string titleKey, string[] optionKeys, int current, Action<int> apply)
    {
        var l = context.Localizer;
        var title = l.Get(titleKey);
        string[] labels = [.. optionKeys.Select(k => l.Get(k))];
        return new ChoiceScreen(title, labels, current, (index, _) =>
        {
            apply(index);
            return ScreenResult.Pop(prefix: l.Get("settings.choice.changed", ("title", title), ("option", labels[index])));
        }, l.Get("settings.choice.current"));
    }

    private static MenuScreen DiagnosticMenu(IModuleContext context)
    {
        var l = context.Localizer;
        return new MenuScreen(l.Get("settings.diagnostic"),
        [
            new MenuItem(l.Get("diagnostic.pronunciation"), _ => ScreenResult.Push(PronunciationTest(context))),
            new MenuItem(l.Get("diagnostic.compare"), _ => ScreenResult.Push(VoiceComparison(context))),
            context.Diagnostics is { } diagnostics
                ? new MenuItem(l.Get("diagnostic.mode"), _ => ScreenResult.Push(DiagnosticMode(context, diagnostics)))
                : NotYetAvailable(l.Get("diagnostic.mode"), context),
            context.Diagnostics is { } copy
                ? new MenuItem(l.Get("diagnostic.copyLog"), _ => ScreenResult.Say(DiagnosticsMessages.CopyToDesktop(copy, l)))
                : NotYetAvailable(l.Get("diagnostic.copyLog"), context),
            context.Settings is { } settings
                ? new MenuItem(l.Get("diagnostic.simulatePower"), _ => ScreenResult.Push(Choice(context, "diagnostic.simulatePower",
                    ["settings.simulatePower.on", "settings.simulatePower.off"],
                    settings.Current.SimulatePower ? 0 : 1, i => settings.SetSimulatePower(i == 0))))
                : NotYetAvailable(l.Get("diagnostic.simulatePower"), context),
        ]);
    }

    /// <summary>On or off; the same as Ctrl+Shift+D. Turning it on starts a new session file.</summary>
    private static ChoiceScreen DiagnosticMode(IModuleContext context, IDiagnosticsService diagnostics)
    {
        var l = context.Localizer;
        return new ChoiceScreen(
            l.Get("diagnostic.mode"),
            [l.Get("diagnostic.mode.on"), l.Get("diagnostic.mode.off")],
            diagnostics.IsOn ? 0 : 1,
            (index, _) => ScreenResult.Pop(prefix: DiagnosticsMessages.Switch(diagnostics, l, index == 0)),
            l.Get("settings.choice.current"));
    }

    /// <summary>The phrases are fixed, so every comparison is made on the same text.</summary>
    private static readonly string[] ComparisonPhraseKeys =
        ["compare.numbers", "compare.diacritics", "compare.long", "compare.question", "compare.dateTime"];

    private static IScreen VoiceComparison(IModuleContext context)
    {
        var l = context.Localizer;
        var title = l.Get("diagnostic.compare");
        if (context.VoiceComparison is not { } comparison) return VoiceComparisonScreen.Unavailable(title, l.Get("compare.unavailable"));
        var phrases = ComparisonPhraseKeys
            .Select(key => (l.Get(key + ".label"), l.Get(key + ".text")))
            .ToList();
        var help = l.Get("compare.help", ("variants", string.Join(", ", comparison.Variants)));
        return new VoiceComparisonScreen(title, phrases, comparison, help);
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
