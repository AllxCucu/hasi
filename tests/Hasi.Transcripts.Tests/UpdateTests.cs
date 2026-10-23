using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Updates;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>Phase 0.11 step 3: updates offered once per version, spoken progress, plain errors.</summary>
public class UpdateTests
{
    private const string Offer =
        "Este disponibilă versiunea 0.2.0 a HASI. Doriți să actualizați acum? HASI se va închide și va porni din nou. Apăsați Enter pentru da sau Escape pentru nu.";

    private sealed class Setup
    {
        public Setup()
        {
            Harness = new ShellHarness();
            Flow = new UpdateFlow(Updates, Harness.Settings, Harness.Shell, Harness.Localizer, NullDiagnosticLog.Instance);
            var context = (ModuleContext)Harness.ModuleContext with { Updates = Flow };
            Harness.Shell.Start(MainMenu.Create([new SettingsModule()], context));
        }

        public ShellHarness Harness { get; }

        public FakeUpdateService Updates { get; } = new();

        public UpdateFlow Flow { get; }

        public string? LastSaid => Harness.LastSaid;
    }

    [Fact]
    public void A_new_version_is_offered_after_the_welcome_without_cutting_it()
    {
        var s = new Setup();
        s.Flow.CheckAtStartup();

        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");

        Assert.Equal(Offer, s.LastSaid);
        Assert.Equal(UtterancePriority.Queue, s.Harness.LastAnnouncement!.Utterance.Priority);
    }

    [Theory]
    [InlineData(UpdateCheckStatus.UpToDate)]
    [InlineData(UpdateCheckStatus.Offline)]
    [InlineData(UpdateCheckStatus.NotInstalled)]
    [InlineData(UpdateCheckStatus.Failed)]
    public void At_startup_nothing_is_said_unless_there_is_a_new_version(UpdateCheckStatus status)
    {
        var s = new Setup();
        var said = s.Harness.Output.Announcements.Count;
        s.Flow.CheckAtStartup();

        s.Updates.FinishCheck(status);

        Assert.Equal(said, s.Harness.Output.Announcements.Count);
    }

    [Fact]
    public void Escape_postpones_and_the_same_version_is_not_offered_again_at_startup()
    {
        var s = new Setup();
        s.Flow.CheckAtStartup();
        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");

        Assert.Equal(["Puteți actualiza oricând din Setări, Actualizări. Meniu principal. Selectat: Setări, 1 din 1."], s.Harness.Press(Key.Escape));

        var said = s.Harness.Output.Announcements.Count;
        s.Flow.CheckAtStartup();
        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");
        Assert.Equal(said, s.Harness.Output.Announcements.Count);
        Assert.Equal(0, s.Updates.Downloads);
    }

    [Fact]
    public void A_newer_version_is_offered_again_once()
    {
        var s = new Setup();
        s.Harness.Settings.Update(u => u with { UpdateOfferedVersion = "0.2.0" });
        s.Flow.CheckAtStartup();

        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.3.0");

        Assert.StartsWith("Este disponibilă versiunea 0.3.0 a HASI.", s.LastSaid, StringComparison.Ordinal);
        Assert.Equal("0.3.0", s.Harness.Settings.Current.UpdateOfferedVersion);
    }

    [Fact]
    public void No_check_at_startup_when_automatic_checking_is_off()
    {
        var s = new Setup();
        s.Harness.Settings.Update(u => u with { AutoCheckUpdates = false });

        s.Flow.CheckAtStartup();

        Assert.Equal(0, s.Updates.Checks);
    }

    [Fact]
    public void Enter_downloads_with_spoken_progress_then_restarts()
    {
        var s = new Setup();
        s.Flow.CheckAtStartup();
        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");

        Assert.Equal(["Se descarcă actualizarea."], s.Harness.Press(Key.Enter));
        var before = s.Harness.Output.Announcements.Count;
        foreach (var percent in new[] { 10, 26, 30, 51, 74, 76, 100 }) s.Updates.Progress(percent);
        Assert.Equal(
            ["Descărcat: 25 la sută.", "Descărcat: 50 la sută.", "Descărcat: 75 la sută."],
            s.Harness.Output.Announcements.Skip(before).Select(a => a.Utterance.Text));
        Assert.False(s.Updates.Applied);

        s.Updates.FinishDownload(ready: true);

        Assert.Equal("Actualizarea este gata. HASI pornește din nou.", s.LastSaid);
        Assert.True(s.Updates.Applied);
    }

    [Fact]
    public void A_failed_download_keeps_the_current_version_and_says_so()
    {
        var s = new Setup();
        s.Flow.CheckAtStartup();
        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");
        s.Harness.Press(Key.Enter);

        s.Updates.FinishDownload(ready: false);

        Assert.Equal("Actualizarea nu a putut fi descărcată. Rămâne versiunea 0.1.0.", s.LastSaid);
        Assert.Equal(Earcon.Error, s.Harness.LastAnnouncement!.Utterance.Earcon);
        Assert.False(s.Updates.Applied);
    }

    /// <summary>Setări (1) → Actualizări (11).</summary>
    private static Setup OnUpdates()
    {
        var s = new Setup();
        s.Harness.Press(Key.Enter, Key.End);
        return s;
    }

    [Fact]
    public void The_updates_screen_lists_check_automatic_and_version()
    {
        var s = OnUpdates();

        Assert.Equal(["Actualizări. 3 opțiuni. Selectat: Caută actualizări acum, 1 din 3."], s.Harness.Press(Key.Enter));
        Assert.Equal(["Verificare automată la pornire, 2 din 3."], s.Harness.Press(Key.Down));
        Assert.Equal(["Versiunea instalată: 0.1.0, 3 din 3."], s.Harness.Press(Key.Down));
        Assert.Equal(["Versiunea instalată: 0.1.0"], s.Harness.Press(Key.Enter));
    }

    [Theory]
    [InlineData(UpdateCheckStatus.UpToDate, "Aveți cea mai nouă versiune a HASI, 0.1.0.")]
    [InlineData(UpdateCheckStatus.Offline, "Nu există conexiune la internet.")]
    [InlineData(UpdateCheckStatus.NotInstalled, "Actualizările funcționează doar după ce HASI este instalat cu programul de instalare.")]
    [InlineData(UpdateCheckStatus.Failed, "Verificarea actualizărilor nu a reușit. Încercați mai târziu.")]
    public void Checking_now_always_answers(UpdateCheckStatus status, string answer)
    {
        var s = OnUpdates();
        s.Harness.Press(Key.Enter);

        Assert.Equal(["Caut actualizări."], s.Harness.Press(Key.Enter));
        s.Updates.FinishCheck(status);

        Assert.Equal(answer, s.LastSaid);
    }

    [Fact]
    public void Checking_now_offers_even_a_version_postponed_before()
    {
        var s = OnUpdates();
        s.Harness.Settings.Update(u => u with { UpdateOfferedVersion = "0.2.0" });
        s.Harness.Press(Key.Enter, Key.Enter);

        s.Updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");

        Assert.Equal(Offer, s.LastSaid);
    }

    [Fact]
    public void Automatic_checking_can_be_turned_off()
    {
        var s = OnUpdates();
        s.Harness.Press(Key.Enter, Key.Down);

        Assert.Equal(["Verificare automată la pornire. 2 opțiuni. Selectat: Da, actual, 1 din 2."], s.Harness.Press(Key.Enter));
        s.Harness.Press(Key.Down);
        Assert.Equal(["Verificare automată la pornire: Nu. Actualizări. Selectat: Verificare automată la pornire, 2 din 3."], s.Harness.Press(Key.Enter));
        Assert.False(s.Harness.Settings.Current.AutoCheckUpdates);
    }

    [Theory]
    [InlineData(null, UpdateSourceKind.GitHub, "https://github.com/AllxCucu/hasi")]
    [InlineData("github", UpdateSourceKind.GitHub, "https://github.com/AllxCucu/hasi")]
    [InlineData("github:https://github.com/alt/hasi", UpdateSourceKind.GitHub, "https://github.com/alt/hasi")]
    [InlineData(@"folder:C:\HASI\versiuni", UpdateSourceKind.Folder, @"C:\HASI\versiuni")]
    [InlineData("http:https://exemplu.ro/hasi", UpdateSourceKind.Web, "https://exemplu.ro/hasi")]
    [InlineData("https://exemplu.ro/hasi", UpdateSourceKind.Web, "https://exemplu.ro/hasi")]
    public void Update_sources_are_read_from_settings_or_the_command_line(string? text, UpdateSourceKind kind, string location)
    {
        Assert.Equal(new UpdateSource(kind, location), UpdateSource.Parse(text));
    }

    [Fact]
    public void An_unknown_update_source_is_refused()
    {
        Assert.Throws<FormatException>(() => UpdateSource.Parse("ftp:undeva"));
    }

    [Theory]
    [InlineData("0.2.0", null, true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.2.0", "0.3.0", false)]
    [InlineData("0.10.0", "0.9.0", true)]
    [InlineData("0.2.0-beta.2", "0.1.0", true)]
    public void Only_a_newer_version_than_the_one_offered_is_offered(string version, string? offered, bool newer)
    {
        Assert.Equal(newer, UpdateFlow.IsNewer(version, offered));
    }
}
