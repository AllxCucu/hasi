using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Modules.Clock;
using Hasi.Testing;
using Hasi.Text.Ro;

namespace Hasi.Transcripts.Tests;

public class ClockModuleTests
{
    private static readonly TimeZoneInfo Romania = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    /// <summary>The Clock module alone, opened, with the clock at <paramref name="instant"/> (UTC).</summary>
    private static (ShellHarness Harness, ManualTimeProvider Time) Opened(string instant, bool realNormalizer = false)
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture), Romania);
        var h = new ShellHarness(time: time, normalizer: realNormalizer
                ? new RomanianNormalizer(AcronymLexicon.Load(AcronymLexicon.DefaultPath), NounGenderLexicon.LoadDefault())
                : null)
            .StartWithModules(new ClockModule());
        h.Press(Key.Enter);
        return (h, time);
    }

    [Fact]
    public void Clock_menu_announces_its_four_items_and_help_mentions_ctrl_t()
    {
        var (h, _) = Opened("2026-10-24T11:32:00Z");
        Assert.Equal("Ceas și calendar. 4 opțiuni. Selectat: Ora, 1 din 4.", h.LastSaid);
        Assert.Contains("Control T spune ora", h.Press(Key.F1)[0], StringComparison.Ordinal);
        Assert.Equal(["Sunteți în: Meniu principal, Ceas și calendar."], h.Press(new KeyChord(Key.U, KeyModifiers.Ctrl)));
    }

    [Theory]
    [InlineData("2026-10-24T11:32:00Z", "Este ora 14:32.", "Este ora paisprezece și treizeci și două de minute.")]
    [InlineData("2026-10-24T12:00:00Z", "Este ora 15:00.", "Este ora cincisprezece.")]
    public void Time_item_says_the_local_time_and_stays_in_the_menu(string instant, string written, string spoken)
    {
        var (h, _) = Opened(instant, realNormalizer: true);
        var menu = h.Shell.Current;
        Assert.Equal([written], h.Press(Key.Enter));
        Assert.Equal(spoken, h.LastAnnouncement!.SpokenText);
        Assert.Same(menu, h.Shell.Current);
    }

    [Theory]
    [InlineData("2026-10-25T00:30:00Z")] // 03:30 summer time, before the clocks go back
    [InlineData("2026-10-25T01:30:00Z")] // 03:30 winter time, one hour later
    public void On_the_night_the_clocks_go_back_both_three_thirties_are_said_as_such(string instant)
    {
        var (h, _) = Opened(instant);
        Assert.Equal(["Este ora 3:30."], h.Press(Key.Enter));
        Assert.Equal(["Data, 2 din 4.", "Astăzi este duminică, 25 octombrie 2026."], h.Press(Key.Down, Key.Enter));
    }

    [Fact]
    public void Date_item_says_today_and_its_holidays()
    {
        var (h, time) = Opened("2026-10-24T11:32:00Z");
        h.Press(Key.Down);
        Assert.Equal(["Astăzi este sâmbătă, 24 octombrie 2026."], h.Press(Key.Enter));

        time.SetUtcNow(DateTimeOffset.Parse("2026-05-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(["Astăzi este vineri, 1 mai 2026. Sărbătoare legală: Ziua Muncii."], h.Press(Key.Enter));

        time.SetUtcNow(DateTimeOffset.Parse("2026-06-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(["Astăzi este luni, 1 iunie 2026. Sărbători legale: Ziua Copilului și Rusaliile ortodoxe, a doua zi."],
            h.Press(Key.Enter));
    }

    [Fact]
    public void Calendar_starts_today_and_names_the_holidays_of_each_day()
    {
        var (h, _) = Opened("2026-04-29T09:00:00Z");
        h.Press(Key.Down, Key.Down);
        Assert.Equal(["Calendar. Miercuri, 29 aprilie 2026, astăzi."], h.Press(Key.Enter));
        Assert.Equal(["Joi, 30 aprilie 2026.", "Vineri, 1 mai 2026. Sărbătoare legală: Ziua Muncii."], h.Press(Key.Down, Key.Down));
        Assert.Equal(["Luni, 1 iunie 2026. Sărbători legale: Ziua Copilului și Rusaliile ortodoxe, a doua zi."],
            h.Press(Key.PageDown));
        Assert.Contains("începând cu anul 2017", h.Press(Key.F1)[0], StringComparison.Ordinal);
        Assert.Equal(["Ceas și calendar. Selectat: Calendar, 3 din 4."], h.Press(Key.Escape));
    }

    [Fact]
    public void Calendar_says_nothing_about_holidays_before_2017()
    {
        var (h, _) = Opened("2026-12-25T09:00:00Z");
        h.Press(Key.Down, Key.Down);
        Assert.Equal(["Calendar. Vineri, 25 decembrie 2026, astăzi. Sărbătoare legală: Crăciunul, prima zi."], h.Press(Key.Enter));
        for (var i = 0; i < 12 * 10; i++) h.Press(Key.PageUp);
        Assert.Equal("Duminică, 25 decembrie 2016.", h.LastSaid);
    }

    [Fact]
    public void Holidays_list_starts_at_the_next_holiday_of_this_year()
    {
        var (h, _) = Opened("2026-10-24T11:32:00Z");
        h.Press(Key.End);
        Assert.Equal(["Sărbători legale 2026. 16 zile de sărbătoare. Selectat: Luni, 30 noiembrie: Sfântul Apostol Andrei, Ocrotitorul României, 13 din 16."],
            h.Press(Key.Enter));
        Assert.Equal(["Sâmbătă, 15 august: Adormirea Maicii Domnului, 12 din 16."], h.Press(Key.Up));
        Assert.Equal(["Luni, 1 iunie: Ziua Copilului și Rusaliile ortodoxe, a doua zi, 11 din 16."], h.Press(Key.Up));
        Assert.Equal(["Sunteți în: Meniu principal, Ceas și calendar, Sărbători legale 2026."],
            h.Press(new KeyChord(Key.U, KeyModifiers.Ctrl)));
    }

    [Fact]
    public void Left_and_right_change_the_year_within_the_known_years()
    {
        var (h, _) = Opened("2026-10-24T11:32:00Z");
        h.Press(Key.End, Key.Enter);
        Assert.Equal(["Sărbători legale 2027. 17 zile de sărbătoare. Selectat: Vineri, 1 ianuarie: Anul Nou, prima zi, 1 din 17."],
            h.Press(Key.Right));
        for (var i = 0; i < 9; i++) h.Press(Key.Left);
        Assert.Equal("Sărbători legale 2018. 15 zile de sărbătoare. Selectat: Luni, 1 ianuarie: Anul Nou, prima zi, 1 din 15.", h.LastSaid);
        h.Press(Key.Left);
        Assert.Equal(["HASI cunoaște sărbătorile legale din anul 2017 până în anul 2099."], h.Press(Key.Left));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal("Sărbători legale 2017", h.Shell.Current.Title);
        Assert.Contains("cultelor necreștine", h.Press(Key.F1)[0], StringComparison.Ordinal);
        Assert.Equal(["Ceas și calendar. Selectat: Sărbători legale, 4 din 4."], h.Press(Key.Escape));
    }

    [Fact]
    public void Holidays_list_selects_the_last_holiday_when_none_is_left_this_year()
    {
        var (h, _) = Opened("2026-12-30T09:00:00Z");
        h.Press(Key.End);
        Assert.EndsWith("Selectat: Sâmbătă, 26 decembrie: Crăciunul, a doua zi, 16 din 16.", h.Press(Key.Enter)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Clock_is_first_in_the_main_menu_of_the_application_order()
    {
        var h = new ShellHarness().StartWithModules(new ClockModule(), new Hasi.Modules.Settings.SettingsModule());
        Assert.Equal("Bun venit în HASI. Meniu principal. 2 opțiuni. Selectat: Ceas și calendar, 1 din 2.", h.LastSaid);
        Assert.IsType<MenuScreen>(h.Shell.Current);
    }
}
