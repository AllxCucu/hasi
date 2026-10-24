using Hasi.Core;
using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Speech;
using Hasi.Modules.About;
using Hasi.Testing;
using Hasi.Text.Ro;

namespace Hasi.Transcripts.Tests;

public class AboutScreenTests
{
    private static ShellHarness Harness(ShellOptions? options = null) => new(options,
        normalizer: new RomanianNormalizer(AcronymLexicon.Load(AcronymLexicon.DefaultPath), NounGenderLexicon.LoadDefault()));

    private static void AssertRhVoice(Announcement announcement)
    {
        Assert.Contains("RHVoice versiunea 1.18.4", announcement.Utterance.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("commit", announcement.Utterance.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("commit", announcement.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("commit fa9dd196", announcement.Utterance.DisplayText!, StringComparison.Ordinal);
        Assert.Equal(announcement.Utterance.Text,
            announcement.Utterance.DisplayText!.Replace(" (commit fa9dd196)", "", StringComparison.Ordinal));
    }

    [Fact]
    public void Rhvoice_commit_is_displayed_but_never_spoken_on_navigation_repeat_or_return()
    {
        var h = Harness().StartWithModules(new AboutModule());
        h.Press(Key.Enter, Key.Down, Key.Down, Key.Down);
        AssertRhVoice(h.LastAnnouncement!);

        foreach (var key in new[] { Key.F2, Key.Enter })
        {
            h.Press(key);
            AssertRhVoice(h.LastAnnouncement!);
        }

        h.Shell.Show(ScreenResult.Confirm(h.Localizer.Get("shell.exit.confirm"), () => ScreenResult.Handled));
        h.Press(Key.Escape);
        AssertRhVoice(h.LastAnnouncement!);

        h.Press(Key.F3);
        Assert.Equal(h.Localizer.Get("shell.whereAmI", ("path", string.Join(", ", h.Shell.Stack.Select(s => s.Title)))), h.LastSaid);
        Assert.Null(h.LastAnnouncement!.Utterance.DisplayText);
    }

    [Theory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    public void Display_label_survives_entry_and_list_boundaries(Key key)
    {
        var h = Harness();
        var about = Assert.IsType<MenuScreen>(new AboutModule().CreateRootScreen(h.ModuleContext));
        h.Start(new MenuScreen(about.Title, [about.Items[3]]));
        AssertRhVoice(h.LastAnnouncement!);

        h.Press(key);
        AssertRhVoice(h.LastAnnouncement!);
        Assert.Equal(key is Key.Up or Key.Down ? Earcon.EdgeOfList : Earcon.None, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Display_label_survives_wrapping()
    {
        var h = Harness(new ShellOptions { WrapLists = true });
        var about = Assert.IsType<MenuScreen>(new AboutModule().CreateRootScreen(h.ModuleContext));
        h.Start(new MenuScreen(about.Title, [about.Items[0], about.Items[3]]));

        h.Press(Key.Up);

        AssertRhVoice(h.LastAnnouncement!);
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Rhvoice_commit_is_absent_from_say_and_speak_diagnostics()
    {
        var h = Harness().StartWithModules(new AboutModule());
        h.Press(Key.Enter, Key.Down, Key.Down, Key.Down);
        using var lines = new StringWriter();
        var log = new TextDiagnosticLog(lines, TimeProvider.System);
        using var engine = new NullSpeechEngine();
        var channel = new SpeechOutputChannel(engine, new SilentEarcons(), log);
        var announcer = new Announcer(new OutputHub([channel], log), h.Normalizer, log);

        announcer.Say(h.LastAnnouncement!.Utterance);

        Assert.Contains("SAY", lines.ToString(), StringComparison.Ordinal);
        Assert.Contains("SPEAK", lines.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("commit", lines.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fa9dd196", lines.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SilentEarcons : IEarconPlayer
    {
        public void Play(Earcon earcon) { }
    }
}
