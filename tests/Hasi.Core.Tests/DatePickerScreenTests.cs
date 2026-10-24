using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class DatePickerScreenTests
{
    private static readonly DateOnly Min = new(1900, 1, 1);
    private static readonly DateOnly Max = new(2099, 12, 31);

    private static (ShellHarness Harness, DatePickerScreen Picker) Open(DateOnly date,
        Func<DateOnly, IScreenContext, ScreenResult>? choose = null, Func<DateOnly, string?>? describe = null,
        DateOnly? min = null, DateOnly? max = null)
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 23, 22, 0, 0, TimeSpan.Zero),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        var picker = new DatePickerScreen("Calendar", date, min ?? Min, max ?? Max, describe ?? (_ => null),
            DateOnly.FromDateTime(time.GetLocalNow().DateTime), choose, "Detalii despre zi.");
        var h = new ShellHarness(time: time).Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(picker));
        return (h, picker);
    }

    [Theory]
    [InlineData("2026-01-31", "Down", "2026-02-01", "Duminică, 1 februarie 2026.")]
    [InlineData("2026-03-01", "Up", "2026-02-28", "Sâmbătă, 28 februarie 2026.")]
    [InlineData("2028-02-28", "Down", "2028-02-29", "Marți, 29 februarie 2028.")]
    [InlineData("2028-03-01", "Up", "2028-02-29", "Marți, 29 februarie 2028.")]
    [InlineData("2026-12-31", "Down", "2027-01-01", "Vineri, 1 ianuarie 2027.")]
    [InlineData("1900-02-28", "Down", "1900-03-01", "Joi, 1 martie 1900.")]
    public void Arrows_move_by_calendar_day(string initial, string key, string expected, string said)
    {
        var (h, picker) = Open(DateOnly.Parse(initial));
        Assert.Equal([said], h.Press(KeyChord.Parse(key)));
        Assert.Equal(DateOnly.Parse(expected), picker.SelectedDate);
    }

    [Theory]
    [InlineData("2026-01-31", "PageDown", "2026-02-28", "Sâmbătă, 28 februarie 2026.")]
    [InlineData("2028-01-31", "NumPad3", "2028-02-29", "Marți, 29 februarie 2028.")]
    [InlineData("2026-03-31", "Right", "2026-04-30", "Joi, 30 aprilie 2026.")]
    [InlineData("2026-03-31", "PageUp", "2026-02-28", "Sâmbătă, 28 februarie 2026.")]
    [InlineData("2028-03-31", "NumPad9", "2028-02-29", "Marți, 29 februarie 2028.")]
    [InlineData("2026-05-31", "Left", "2026-04-30", "Joi, 30 aprilie 2026.")]
    [InlineData("2026-12-15", "PageDown", "2027-01-15", "Vineri, 15 ianuarie 2027.")]
    [InlineData("2026-01-15", "PageUp", "2025-12-15", "Luni, 15 decembrie 2025.")]
    public void Month_moves_clamp_the_day(string initial, string key, string expected, string said)
    {
        var (h, picker) = Open(DateOnly.Parse(initial));
        Assert.Equal([said], h.Press(KeyChord.Parse(key)));
        Assert.Equal(DateOnly.Parse(expected), picker.SelectedDate);
    }

    [Theory]
    [InlineData("Home", 1)]
    [InlineData("Ctrl+Up", 1)]
    [InlineData("NumPad7", 1)]
    [InlineData("End", 29)]
    [InlineData("Ctrl+Down", 29)]
    [InlineData("NumPad1", 29)]
    public void First_and_last_stay_in_the_month(string key, int day)
    {
        var (h, picker) = Open(new DateOnly(2028, 2, 15));
        h.Press(KeyChord.Parse(key));
        Assert.Equal(new DateOnly(2028, 2, day), picker.SelectedDate);
        Assert.Contains($", {day} februarie 2028.", h.LastSaid);
    }

    [Theory]
    [InlineData("1900-01-01", "Up", "Începutul calendarului.")]
    [InlineData("1900-01-01", "Left", "Începutul calendarului.")]
    [InlineData("1900-01-01", "PageUp", "Începutul calendarului.")]
    [InlineData("2099-12-31", "Down", "Sfârșitul calendarului.")]
    [InlineData("2099-12-31", "Right", "Sfârșitul calendarului.")]
    [InlineData("2099-12-31", "PageDown", "Sfârșitul calendarului.")]
    public void Limits_have_specific_text_and_an_earcon_even_with_wrapping(string date, string key, string edge)
    {
        var initial = DateOnly.Parse(date);
        var (h, picker) = Open(initial);
        h.Shell.Options.WrapLists = true;
        h.Press(KeyChord.Parse(key));
        Assert.Equal(initial, picker.SelectedDate);
        Assert.StartsWith(edge, h.LastSaid);
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Theory]
    [InlineData("0001-01-01", "PageUp")]
    [InlineData("0001-01-01", "Up")]
    [InlineData("9999-12-31", "PageDown")]
    [InlineData("9999-12-31", "Down")]
    public void Generic_picker_also_handles_DateOnly_extremes(string date, string key)
    {
        var initial = DateOnly.Parse(date);
        var (h, picker) = Open(initial, min: DateOnly.MinValue, max: DateOnly.MaxValue);
        h.Press(KeyChord.Parse(key));
        Assert.Equal(initial, picker.SelectedDate);
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Partial_month_bounds_clamp_navigation()
    {
        var (h, picker) = Open(new DateOnly(2026, 5, 15), min: new DateOnly(2026, 5, 10), max: new DateOnly(2026, 6, 20));
        h.Press(Key.Home);
        Assert.Equal(new DateOnly(2026, 5, 10), picker.SelectedDate);
        Assert.StartsWith("Începutul calendarului.", h.LastSaid);
        h.Press(Key.PageDown, Key.End);
        Assert.Equal(new DateOnly(2026, 6, 20), picker.SelectedDate);
        Assert.StartsWith("Sfârșitul calendarului.", h.LastSaid);
    }

    [Fact]
    public void Entry_marks_today_using_the_supplied_local_date_and_enter_repeats_it()
    {
        var (h, picker) = Open(new DateOnly(2026, 10, 24));
        Assert.Equal("Calendar. Sâmbătă, 24 octombrie 2026, astăzi.", h.LastSaid);
        Assert.Equal(["Sâmbătă, 24 octombrie 2026, astăzi."], h.Press(Key.Enter));
        Assert.Same(picker, h.Shell.Current);
        h.Press(Key.Down);
        Assert.DoesNotContain("astăzi", h.LastSaid);
        h.Press(Key.Escape);
        Assert.IsType<MenuScreen>(h.Shell.Current);
    }

    [Fact]
    public void Caller_description_is_appended_once_and_choose_receives_the_selected_day_and_context()
    {
        DateOnly? chosen = null;
        IScreenContext? received = null;
        var calls = 0;
        var (h, _) = Open(new DateOnly(2026, 5, 1), (date, context) =>
        {
            chosen = date;
            received = context;
            return ScreenResult.Pop();
        }, _ => { calls++; return "Sărbătoare legală: Ziua Muncii."; });
        Assert.Equal("Calendar. Vineri, 1 mai 2026. Sărbătoare legală: Ziua Muncii.", h.LastSaid);
        Assert.Equal(1, calls);
        h.Press(Key.Down, Key.Enter);
        Assert.Equal(new DateOnly(2026, 5, 2), chosen);
        Assert.Same(h.Shell, received);
        Assert.IsType<MenuScreen>(h.Shell.Current);
    }

    [Fact]
    public void Help_describes_navigation_and_appends_caller_help_without_moving()
    {
        var (h, picker) = Open(new DateOnly(2026, 10, 24));
        h.Press(new KeyChord(Key.H, KeyModifiers.Ctrl));
        Assert.Equal(h.Localizer.Get("datePicker.help") + " Detalii despre zi.", h.LastSaid);
        Assert.Equal(new DateOnly(2026, 10, 24), picker.SelectedDate);
        Assert.Empty(h.Localizer.MissingKeys);
    }

    [Fact]
    public void Invalid_ranges_or_initial_dates_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatePickerScreen("Calendar", Min, Max, Min, _ => null, Min));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatePickerScreen("Calendar", Min.AddDays(-1), Min, Max, _ => null, Min));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatePickerScreen("Calendar", Max.AddDays(1), Min, Max, _ => null, Min));
    }

    [Theory]
    [InlineData(Key.PageDown)]
    [InlineData(Key.PageUp)]
    public void Page_commands_remain_unavailable_on_other_screens(Key key)
    {
        IScreen[] screens = [TestScreens.Fruits(), new MessageScreen("Title", "Message"),
            new TextInputScreen("Title", "Prompt", (_, _) => ScreenResult.Handled),
            new ValueScreen("Title", () => 5, _ => { }, 0, 10, 1, v => v.ToString()),
            new ConfirmScreen("Question", () => ScreenResult.Handled, "Title")];
        foreach (var screen in screens)
        {
            var h = new ShellHarness().Start(screen);
            h.Press(key);
            Assert.Equal(h.Localizer.Get("shell.keyUnavailable"), h.LastSaid);
            Assert.Equal(Earcon.Unavailable, h.LastAnnouncement!.Utterance.Earcon);
            Assert.Same(screen, h.Shell.Current);
        }
    }
}
