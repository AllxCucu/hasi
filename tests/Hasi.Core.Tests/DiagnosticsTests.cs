using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class DiagnosticsTests
{
    private static ShellHarness Started() => new ShellHarness().Start(TestScreens.Fruits());

    [Theory]
    [InlineData(Key.D)]
    [InlineData(Key.F12)]
    public void Ctrl_shift_switches_diagnostic_mode_on_and_off_on_any_screen(Key key)
    {
        var h = Started();
        var chord = new KeyChord(key, KeyModifiers.Ctrl | KeyModifiers.Shift);

        Assert.Equal(["Modul de diagnostic este pornit. Tastele și mesajele se scriu într-un jurnal, doar pe acest calculator."], h.Press(chord));
        Assert.True(h.Diagnostics.IsOn);
        Assert.Equal(["Modul de diagnostic este oprit."], h.Press(chord));
        Assert.False(h.Diagnostics.IsOn);
        Assert.Single(h.Shell.Stack);
    }

    [Fact]
    public void A_log_that_cannot_be_written_is_said_plainly()
    {
        var h = Started();
        h.Diagnostics.FailToStart = true;

        Assert.Equal(["Modul de diagnostic nu a putut porni, pentru că jurnalul nu poate fi scris."],
            h.Press(new KeyChord(Key.D, KeyModifiers.Ctrl | KeyModifiers.Shift)));
        Assert.Equal(Output.Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);
        Assert.False(h.Diagnostics.IsOn);
    }

    [Theory]
    [InlineData(LogCopyResult.Copied, "Jurnalul a fost copiat pe Desktop.")]
    [InlineData(LogCopyResult.NothingToCopy, "Nu există încă niciun jurnal. Porniți mai întâi modul de diagnostic, apoi folosiți HASI ca de obicei.")]
    [InlineData(LogCopyResult.Failed, "Jurnalul nu a putut fi copiat pe Desktop.")]
    public void Copying_the_log_says_what_happened(LogCopyResult result, string expected)
    {
        var diagnostics = new FakeDiagnosticsService { CopyResult = result };
        var h = new ShellHarness();

        Assert.Equal(expected, DiagnosticsMessages.CopyToDesktop(diagnostics, h.Localizer).Text);
        Assert.Equal(1, diagnostics.Copies);
    }

    [Fact]
    public void The_switchable_log_writes_only_while_attached()
    {
        var log = new SwitchableDiagnosticLog();
        var lines = new StringWriter();
        log.Write("KEY", "before");

        var previous = log.Attach(new TextDiagnosticLog(lines, TimeProvider.System));
        log.Write("KEY", "during");
        var detached = log.Detach();
        log.Write("KEY", "after");

        Assert.IsType<NullDiagnosticLog>(previous);
        Assert.IsType<TextDiagnosticLog>(detached);
        Assert.False(log.IsEnabled);
        var text = lines.ToString();
        Assert.Contains("KEY    during", text, StringComparison.Ordinal);
        Assert.DoesNotContain("before", text, StringComparison.Ordinal);
        Assert.DoesNotContain("after", text, StringComparison.Ordinal);
    }
}

public sealed class ErrorLogFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hasi-errors-" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_folder, "logs", "errors.log");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void An_entry_has_the_time_the_context_the_version_and_the_indented_exception()
    {
        var when = new DateTimeOffset(2026, 10, 23, 14, 30, 5, 120, TimeSpan.FromHours(3));
        Exception error;
        try
        {
            throw new InvalidOperationException("Ceva nu a mers.", new FormatException("cauza"));
        }
        catch (InvalidOperationException ex)
        {
            error = ex;
        }

        var lines = ErrorLogFile.Format(when, "HASI 0.1.0, Windows 11", "unhandled UI exception", error).Split(Environment.NewLine);

        Assert.Equal("2026-10-23 14:30:05.120 +03:00 ERROR unhandled UI exception (HASI 0.1.0, Windows 11)", lines[0]);
        Assert.Equal("    System.InvalidOperationException: Ceva nu a mers.", lines[1]);
        Assert.Contains(lines, l => l.StartsWith("     ---> System.FormatException: cauza", StringComparison.Ordinal));
        Assert.All(lines.Skip(1).Where(l => l.Length > 0), l => Assert.StartsWith("    ", l, StringComparison.Ordinal));
        Assert.Equal("", lines[^1]);
    }

    [Fact]
    public void Entries_are_appended_and_the_folder_is_created()
    {
        var log = new ErrorLogFile(LogPath, "HASI", TimeProvider.System);

        log.Write("first");
        log.Write("second", new InvalidOperationException("x"));

        var text = File.ReadAllText(LogPath);
        Assert.Contains("ERROR first (HASI)", text, StringComparison.Ordinal);
        Assert.Contains("ERROR second (HASI)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_full_log_is_kept_as_the_old_one_and_a_new_one_starts()
    {
        var log = new ErrorLogFile(LogPath, "HASI", TimeProvider.System, maxBytes: 200);
        log.Write("one", new InvalidOperationException(new string('a', 150)));

        log.Write("two");

        Assert.Contains("ERROR one", File.ReadAllText(log.OldPath), StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR one", File.ReadAllText(LogPath), StringComparison.Ordinal);
        Assert.Contains("ERROR two", File.ReadAllText(LogPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Writing_never_throws_even_where_it_cannot_write()
    {
        var log = new ErrorLogFile(Path.Combine(_folder, "\0invalid", "errors.log"), "HASI", TimeProvider.System);

        log.Write("anything", new InvalidOperationException());
    }
}

public class CrashPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_errors_are_survived_the_third_within_thirty_seconds_is_serious()
    {
        var burst = new ErrorBurst();

        Assert.False(burst.Add(Now));
        Assert.False(burst.Add(Now.AddSeconds(10)));
        Assert.True(burst.Add(Now.AddSeconds(29)));
    }

    [Fact]
    public void Errors_far_apart_never_make_a_burst()
    {
        var burst = new ErrorBurst();

        Assert.False(burst.Add(Now));
        Assert.False(burst.Add(Now.AddSeconds(20)));
        Assert.False(burst.Add(Now.AddSeconds(50)));
        Assert.False(burst.Add(Now.AddSeconds(100)));
    }

    [Fact]
    public void Hasi_restarts_at_most_three_times_in_ten_minutes()
    {
        Assert.True(CrashPolicy.MayRestart([], Now));
        Assert.True(CrashPolicy.MayRestart([Now.AddMinutes(-2), Now.AddMinutes(-1)], Now));
        Assert.False(CrashPolicy.MayRestart([Now.AddMinutes(-3), Now.AddMinutes(-2), Now.AddMinutes(-1)], Now));
        Assert.True(CrashPolicy.MayRestart([Now.AddMinutes(-30), Now.AddMinutes(-20), Now.AddMinutes(-11)], Now));
    }
}
