using Hasi.Core.Output;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>Said after the welcome; the Narrator detector is simulated by <see cref="StartupFacts"/>.</summary>
public class StartupNoticesTests
{
    private static readonly Localization.ILocalizer Localizer = new ShellHarness().Localizer;

    [Fact]
    public void Nothing_to_say_on_a_normal_start()
    {
        Assert.Empty(StartupNotices.For(new StartupFacts(), Localizer));
    }

    [Fact]
    public void A_running_narrator_is_announced_with_how_to_stop_it()
    {
        var notice = Assert.Single(StartupNotices.For(new StartupFacts(NarratorRunning: true), Localizer));

        Assert.Equal(
            "Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta Windows și Enter.",
            notice.Text);
        Assert.Equal(UtterancePriority.Queue, notice.Priority);
    }

    [Fact]
    public void Every_notice_is_queued_most_important_first()
    {
        var notices = StartupNotices.For(new StartupFacts(true, true, true, true), Localizer);

        Assert.Equal(
            [
                "HASI a fost repornit după o problemă neașteptată. Ne cerem scuze.",
                "Naratorul Windows este pornit și va vorbi peste HASI. Pentru a-l opri, apăsați împreună Control, tasta Windows și Enter.",
                "Atenție: repornirea și oprirea calculatorului sunt reale.",
                "Tasta Control Alt H este folosită de alt program, așa că nu vă poate aduce înapoi în HASI.",
            ],
            notices.Select(n => n.Text));
        Assert.All(notices, n => Assert.Equal(UtterancePriority.Queue, n.Priority));
    }
}
