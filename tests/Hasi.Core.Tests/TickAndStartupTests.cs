using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class TickAndStartupTests
{
    private static ManualTimeProvider Clock(string instant) => new(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture),
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));

    private sealed class TickingScreen(string title) : IScreen, ITickingScreen
    {
        public List<DateTimeOffset> Ticks { get; } = [];

        public string Title { get; } = title;

        public Utterance Enter(EnterReason reason, IScreenContext context) => Title;

        public ScreenResult Handle(InputEvent input, IScreenContext context) => ScreenResult.Unhandled;

        public Utterance Help(IScreenContext context) => Title;

        public void Tick(DateTimeOffset now) => Ticks.Add(now);
    }

    private sealed class NoticeModule(string title, Func<IModuleContext, ScreenResult?> onStartup) : IModule
    {
        public int Asked { get; private set; }

        public string Id => title;

        public string TitleKey => "about.title";

        public IScreen CreateRootScreen(IModuleContext context) => new MessageScreen(title, title);

        public ScreenResult? OnStartup(IModuleContext context)
        {
            Asked++;
            return onStartup(context);
        }
    }

    [Fact]
    public void Every_open_ticking_screen_gets_the_tick_even_with_the_hourly_chime_off()
    {
        var time = Clock("2026-10-24T09:15:00Z");
        var below = new TickingScreen("Jos");
        var above = new TickingScreen("Sus");
        var h = new ShellHarness(time: time).Start(below);
        h.Shell.Show(ScreenResult.Push(new MessageScreen("Mesaj", "Un mesaj.")));
        h.Shell.Show(ScreenResult.Push(above));
        var said = h.Output.Announcements.Count;

        h.Shell.ClockTick();
        time.Advance(TimeSpan.FromSeconds(5));
        h.Shell.ClockTick();

        Assert.False(h.Shell.Options.HourlyChime);
        Assert.Equal([time.GetLocalNow().AddSeconds(-5), time.GetLocalNow()], above.Ticks);
        Assert.Equal(2, below.Ticks.Count);
        Assert.Equal(TimeSpan.FromHours(3), above.Ticks[0].Offset); // Local time.
        Assert.Equal(said, h.Output.Announcements.Count);
    }

    [Fact]
    public void Ticks_come_before_the_hourly_announcement()
    {
        var time = Clock("2026-10-24T12:00:03Z");
        var screen = new TickingScreen("Ecran");
        var h = new ShellHarness(new ShellOptions { HourlyChime = true }, time: time).Start(screen);

        h.Shell.ClockTick();

        Assert.Single(screen.Ticks);
        Assert.Equal("Este ora 15:00.", h.LastSaid);
    }

    [Fact]
    public void Startup_notices_follow_the_welcome_queued_in_module_order()
    {
        var first = new NoticeModule("Unu", _ => ScreenResult.Say("Am găsit un document nesalvat."));
        var silent = new NoticeModule("Doi", _ => null);
        var third = new NoticeModule("Trei", _ => ScreenResult.Say("Al treilea modul are ceva de spus."));
        var h = new ShellHarness();

        h.StartWithModules(first, silent, third);

        var said = h.Output.Announcements.Select(a => a.Utterance.Text).ToList();
        Assert.Equal(3, said.Count);
        Assert.StartsWith("Bun venit în HASI. Meniu principal.", said[0], StringComparison.Ordinal);
        Assert.Equal(["Am găsit un document nesalvat.", "Al treilea modul are ceva de spus."], said.Skip(1));
        Assert.All(h.Output.Announcements.Skip(1), a => Assert.Equal(UtterancePriority.Queue, a.Utterance.Priority));
        Assert.Equal((1, 1, 1), (first.Asked, silent.Asked, third.Asked));
    }

    [Fact]
    public void A_startup_notice_may_open_a_screen_over_the_main_menu()
    {
        var module = new NoticeModule("Editor", _ => ScreenResult.Push(new MessageScreen("Document găsit", "Am găsit un document nesalvat.")));
        var h = new ShellHarness();

        h.StartWithModules(module);

        Assert.Equal("Document găsit", h.Shell.Current.Title);
        Assert.Equal("Document găsit. Am găsit un document nesalvat.", h.LastSaid);
        Assert.Equal(UtterancePriority.Queue, h.LastAnnouncement!.Utterance.Priority);
        Assert.Equal(["Meniu principal. Selectat: Despre HASI, 1 din 1."], h.Press(Key.Escape));
    }

    [Fact]
    public void Modules_without_a_notice_change_nothing()
    {
        var h = new ShellHarness();

        h.StartWithModules(new TestScreens.FakeModule("about.title", _ => new MessageScreen("x", "y")));

        Assert.Single(h.Output.Announcements);
    }
}
