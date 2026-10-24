using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Screens;
using Hasi.Core.Output;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class ClockTests
{
    private static ManualTimeProvider Clock(string instant) => new(DateTimeOffset.Parse(instant),
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));

    [Fact]
    public void Shell_and_module_context_share_the_clock_and_default_to_system()
    {
        var time = Clock("2026-10-24T11:32:00Z");
        var h = new ShellHarness(time: time);
        Assert.Same(time, h.Shell.Time);
        Assert.Same(time, h.ModuleContext.Time);
        var defaults = new ShellHarness();
        Assert.Same(TimeProvider.System, defaults.Shell.Time);
        Assert.Same(TimeProvider.System, defaults.ModuleContext.Time);
    }

    [Fact]
    public void Hourly_chime_is_off_by_default()
    {
        var h = new ShellHarness(time: Clock("2026-10-24T12:00:03Z")).Start(TestScreens.Fruits());
        var before = h.Output.Announcements.Count;
        h.Shell.ClockTick();
        Assert.False(h.Shell.Options.HourlyChime);
        Assert.Equal(before, h.Output.Announcements.Count);
    }

    [Fact]
    public void A_new_hour_is_queued_once_without_changing_selection_or_interrupting()
    {
        var time = Clock("2026-10-24T11:59:58Z");
        var h = new ShellHarness(new ShellOptions { HourlyChime = true }, time: time).Start(TestScreens.Fruits());
        h.Press(Key.Down);
        var screen = h.Shell.Current;
        var before = h.Output.Announcements.Count;
        var cancelled = h.Output.CancelCount;
        h.Shell.ClockTick();
        Assert.Equal(before, h.Output.Announcements.Count);
        time.Advance(TimeSpan.FromSeconds(5));
        h.Shell.ClockTick();
        Assert.Equal("Este ora 15:00.", h.LastSaid);
        Assert.Equal(UtterancePriority.Queue, h.LastAnnouncement!.Utterance.Priority);
        time.Advance(TimeSpan.FromSeconds(5));
        h.Shell.ClockTick();
        Assert.Equal(before + 1, h.Output.Announcements.Count);
        Assert.Equal(cancelled, h.Output.CancelCount);
        Assert.Same(screen, h.Shell.Current);
        Assert.Equal(1, ((MenuScreen)screen).SelectedIndex);
        Assert.Equal(["Este ora 15:00."], h.Press(Key.F2));
    }

    [Theory]
    [InlineData("2026-10-24T12:01:59.999Z", true)]
    [InlineData("2026-10-24T12:02:00Z", false)]
    [InlineData("2026-10-24T12:05:00Z", false)]
    public void Only_fresh_hours_are_announced(string instant, bool announced)
    {
        var time = Clock("2026-10-24T11:59:58Z");
        var h = new ShellHarness(new ShellOptions { HourlyChime = true }, time: time).Start(TestScreens.Fruits());
        var before = h.Output.Announcements.Count;
        time.SetUtcNow(DateTimeOffset.Parse(instant));
        h.Shell.ClockTick();
        Assert.Equal(before + (announced ? 1 : 0), h.Output.Announcements.Count);
    }

    [Theory]
    [InlineData("2026-10-25T00:00:03Z", "Este ora 3:00.", "Este ora 3:00.", "Este ora 4:00.")]
    [InlineData("2027-03-28T00:00:03Z", "Este ora 2:00.", "Este ora 4:00.", "Este ora 5:00.")]
    public void Daylight_saving_changes_follow_real_hours(string instant, string first, string second, string third)
    {
        var time = Clock(instant);
        var h = new ShellHarness(new ShellOptions { HourlyChime = true }, time: time).Start(TestScreens.Fruits());
        var before = h.Output.Announcements.Count;
        foreach (var expected in new[] { first, second, third })
        {
            h.Shell.ClockTick();
            Assert.Equal(expected, h.LastSaid);
            h.Shell.ClockTick();
            time.Advance(TimeSpan.FromHours(1));
        }
        Assert.Equal(before + 3, h.Output.Announcements.Count);
        Assert.All(h.Output.Announcements.Skip(before), a => Assert.Equal(UtterancePriority.Queue, a.Utterance.Priority));
    }

    [Fact]
    public void Date_text_uses_resource_names_and_can_capitalize_the_weekday()
    {
        var h = new ShellHarness();
        var date = new DateOnly(2026, 10, 24);
        Assert.Equal("sâmbătă, 24 octombrie 2026", DateText.Date(date, h.Localizer));
        Assert.Equal("Sâmbătă, 24 octombrie 2026", DateText.Date(date, h.Localizer, capitalize: true));
        for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
            Assert.DoesNotContain("[date.", DateText.Date(day, h.Localizer));
        Assert.Empty(h.Localizer.MissingKeys);
    }
}
