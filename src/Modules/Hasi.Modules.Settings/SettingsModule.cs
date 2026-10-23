using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;

namespace Hasi.Modules.Settings;

/// <summary>
/// User preferences (phase 0.6): voice, rate, volume, pitch, key echo, colours, text size and list ends,
/// saved at once and applied immediately. Autostart (0.10), screen reader (0.9) and updates (0.11) follow later.
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
            Item("settings.autostart", null),
            Item("settings.screenReader", null),
            new MenuItem(l.Get("settings.diagnostic"), _ => ScreenResult.Push(DiagnosticMenu(context))),
            Item("settings.updates", null),
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

    private static ChoiceScreen VoiceChoice(IModuleContext context, ISettingsService settings)
    {
        var l = context.Localizer;
        var voices = settings.Voices;
        var current = Math.Max(0, voices.ToList().FindIndex(v => v.Label == settings.CurrentVoiceLabel));
        return new ChoiceScreen(
            l.Get("settings.voice"),
            [.. voices.Select(v => v.Label)],
            current,
            (index, _) => settings.SelectVoice(voices[index])
                ? ScreenResult.Pop(prefix: l.Get("settings.voice.changed", ("voice", voices[index].Label)))
                : ScreenResult.Say(new Utterance(
                    l.Get("settings.voice.failed", ("voice", voices[index].Label), ("current", settings.CurrentVoiceLabel)),
                    Earcon: Earcon.Error)),
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
            NotYetAvailable(l.Get("diagnostic.mode"), context),
            NotYetAvailable(l.Get("diagnostic.copyLog"), context),
        ]);
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
