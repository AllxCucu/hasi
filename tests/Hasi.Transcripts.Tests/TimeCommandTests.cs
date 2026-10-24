using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;
using Hasi.Text.Ro;

namespace Hasi.Transcripts.Tests;

public class TimeCommandTests
{
    private static readonly KeyChord SayTime = new(Key.T, KeyModifiers.Ctrl);

    private static ShellHarness Started(string instant = "2026-10-24T11:32:00Z", IDiagnosticLog? log = null)
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse(instant), TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        return new ShellHarness(time: time, diagnosticLog: log,
                normalizer: new RomanianNormalizer(AcronymLexicon.Load(AcronymLexicon.DefaultPath), NounGenderLexicon.LoadDefault()))
            .StartWithModules(new SettingsModule(), new KeyTutorModule(), new ExitModule());
    }

    [Theory]
    [InlineData("2026-10-24T11:32:00Z", "Este ora 14:32.", "Este ora paisprezece și treizeci și două de minute.")]
    [InlineData("2026-10-24T12:00:00Z", "Este ora 15:00.", "Este ora cincisprezece.")]
    [InlineData("2026-10-23T21:05:00Z", "Este ora 0:05.", "Este ora zero și cinci minute.")]
    [InlineData("2026-10-24T06:05:00Z", "Este ora 9:05.", "Este ora nouă și cinci minute.")]
    public void Time_uses_local_clock_and_the_real_normalizer(string instant, string written, string spoken)
    {
        var h = Started(instant);
        var menu = (MenuScreen)h.Shell.Current;
        Assert.Equal([written], h.Press(SayTime));
        Assert.Equal(spoken, h.LastAnnouncement!.SpokenText);
        Assert.Equal(UtterancePriority.Interrupt, h.LastAnnouncement.Utterance.Priority);
        Assert.Same(menu, h.Shell.Current);
        Assert.Equal(0, menu.SelectedIndex);
    }

    [Fact]
    public void Time_works_in_a_submenu_text_input_and_confirmation_without_changing_them()
    {
        var h = Started();
        h.Press(Key.Enter, Key.Down);
        var submenu = (MenuScreen)h.Shell.Current;
        Assert.Equal(["Este ora 14:32."], h.Press(SayTime));
        Assert.Same(submenu, h.Shell.Current);
        Assert.Equal(1, submenu.SelectedIndex);

        h.Press(Key.End, Key.Up, Key.Enter, Key.Enter);
        var input = Assert.IsType<TextInputScreen>(h.Shell.Current);
        h.Type("text");
        Assert.Equal(["Este ora 14:32."], h.Press(SayTime));
        Assert.Same(input, h.Shell.Current);
        Assert.Equal("text", input.Text);

        h.Press(new KeyChord(Key.F4, KeyModifiers.Alt));
        var confirmation = Assert.IsType<ConfirmScreen>(h.Shell.Current);
        Assert.Equal(["Este ora 14:32."], h.Press(SayTime));
        Assert.Same(confirmation, h.Shell.Current);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Tutor_describes_time_and_page_keys_instead_of_executing_them()
    {
        var h = Started();
        h.Press(Key.Down, Key.Enter);
        var tutor = h.Shell.Current;
        Assert.Equal(["Control plus litera T: spune ora."], h.Press(SayTime));
        Assert.Equal(["Page Down: trece la luna următoare în calendar."], h.Press(Key.PageDown));
        Assert.Equal(["Page Up: trece la luna anterioară în calendar."], h.Press(Key.PageUp));
        Assert.Same(tutor, h.Shell.Current);
        Assert.Empty(h.Localizer.MissingKeys);
    }

    [Fact]
    public void Diagnostic_log_records_SayTime_and_unmasked_time()
    {
        using var text = new StringWriter();
        using var log = new TextDiagnosticLog(text, TimeProvider.System);
        var h = Started(log: log);
        h.Press(SayTime);
        Assert.Contains("CMD    SayTime", text.ToString());
        Assert.Contains("Este ora 14:32.", text.ToString());
        Assert.DoesNotContain(TextDiagnosticLog.Mask, text.ToString());
    }

    [Fact]
    public void First_day_of_month_is_spoken_as_intai()
    {
        var h = Started();
        var date = DateText.Date(new DateOnly(2026, 5, 1), h.Localizer);
        Assert.Equal("vineri, 1 mai 2026", date);
        Assert.Equal("vineri, întâi mai două mii douăzeci și șase", h.Normalizer.Normalize(date));
    }

    [Fact]
    public void Queued_hour_uses_the_real_normalizer()
    {
        var h = Started("2026-10-24T12:00:03Z");
        h.Settings.Update(s => s with { HourlyChime = true });
        h.Shell.ClockTick();
        Assert.Equal("Este ora cincisprezece.", h.LastAnnouncement!.SpokenText);
        Assert.Equal(UtterancePriority.Queue, h.LastAnnouncement.Utterance.Priority);
    }
}
