using Hasi.Core.Input;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;
using Hasi.Text.Ro;

namespace Hasi.Transcripts.Tests;

/// <summary>M0-33 with the real Romanian normalizer: what is spoken and what the screen shows.</summary>
public class PronunciationTestTests
{
    private static ShellHarness OpenPronunciationTest()
    {
        var h = new ShellHarness(normalizer: new RomanianNormalizer())
            .StartWithModules(new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule());
        h.Press(Key.Enter, Key.End, Key.Up, Key.Enter, Key.Enter);
        return h;
    }

    [Fact]
    public void M0_33_speaks_the_normalized_text_and_shows_both_forms()
    {
        var h = OpenPronunciationTest();
        h.Type("Plata de 12,50 lei pe 1.03.2026 la 14:30");

        h.Press(Key.Enter);

        const string spoken = "Plata de doisprezece lei și cincizeci de bani pe întâi martie două mii douăzeci și șase " +
                              "la ora paisprezece și treizeci de minute";
        var said = h.LastAnnouncement!;
        Assert.Equal("Plata de 12,50 lei pe 1.03.2026 la 14:30", said.Utterance.Text);
        Assert.Equal(spoken, said.SpokenText);
        Assert.Equal($"Scris: Plata de 12,50 lei pe 1.03.2026 la 14:30\nRostit: {spoken}", said.Utterance.DisplayText);
    }

    [Fact]
    public void Typed_digits_are_echoed_as_words()
    {
        var h = OpenPronunciationTest();

        h.Type("7");

        Assert.Equal("șapte", h.LastAnnouncement!.SpokenText);
    }

    [Fact]
    public void Typed_cedilla_letters_are_spoken_with_comma_below()
    {
        var h = OpenPronunciationTest();
        h.Type("ţară");

        h.Press(Key.Enter);

        Assert.Equal("țară", h.LastAnnouncement!.SpokenText);
    }
}
