using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Speech;
using Hasi.Core.Storage;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>Findings of the Codex review of module 2's core changes (closing, privacy, backups).</summary>
public class CodexReviewFixesTests
{
    private sealed class PrivateScreen() : MenuScreen("Scrisoare către Ana", [new MenuItem("Rând")]), ISensitiveScreen;

    [Fact]
    public void After_the_exit_is_confirmed_keys_no_longer_reach_any_screen()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Shell.Show(h.Shell.ConfirmExit());
        h.Press(Key.Enter);
        Assert.True(h.System.ExitRequested);
        Assert.True(h.Shell.IsClosing);

        Assert.Empty(h.Press(Key.Down, Key.F1));
        Assert.Empty(h.Type("text pierdut"));
    }

    [Fact]
    public void Keys_still_work_when_the_exit_is_cancelled()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Shell.Show(h.Shell.ConfirmExit());
        h.Press(Key.Escape);

        Assert.False(h.Shell.IsClosing);
        Assert.Equal(["Pară, 2 din 3."], h.Press(Key.Down));
    }

    [Fact]
    public void Every_title_from_the_first_private_screen_up_is_masked_in_the_log()
    {
        var lines = new StringWriter();
        var h = new ShellHarness(diagnosticLog: new TextDiagnosticLog(lines, TimeProvider.System)).Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(new PrivateScreen()));
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Salvează Scrisoare către Ana", [new MenuItem("Da")])));

        var log = lines.ToString();
        Assert.DoesNotContain("Ana", log, StringComparison.Ordinal);
        Assert.Contains("SCREEN Fructe > *** > ***", log.Replace("  ", " ", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    private sealed class SilentEarcons : IEarconPlayer
    {
        public void Play(Earcon earcon) { }
    }

    /// <summary>An engine whose messages end only when the test says so.</summary>
    private sealed class ManualEngine : ISpeechEngine
    {
        public readonly List<long> Pending = [];

        public string Id => "manual";

        public string? CurrentVoiceName => "test";

        public bool IsSpeaking => Pending.Count > 0;

        public event EventHandler<Exception>? Failed { add { } remove { } }

        public event EventHandler<SpeechFinished>? Finished;

        public void Speak(string text, long id) => Pending.Add(id);

        public void Stop()
        {
            foreach (var id in Pending.ToList()) Finish(id, SpeechOutcome.Cancelled);
        }

        public void Finish(long id, SpeechOutcome outcome)
        {
            Pending.Remove(id);
            Finished?.Invoke(this, new SpeechFinished(id, outcome));
        }

        public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

        public void Dispose() { }
    }

    [Fact]
    public void A_private_message_stays_private_until_it_has_been_heard()
    {
        var engine = new ManualEngine();
        var channel = new SpeechOutputChannel(engine, new SilentEarcons(), NullDiagnosticLog.Instance);

        channel.Present(new Announcement(new Utterance("Ana"), "Ana", 1));
        Assert.False(channel.SpeakingPrivate);

        channel.Present(new Announcement(new Utterance("Scrisoare către Ana", Sensitive: true), "Scrisoare către Ana", 2));
        Assert.True(channel.SpeakingPrivate);

        engine.Finish(1, SpeechOutcome.Completed);
        Assert.True(channel.SpeakingPrivate);
        engine.Finish(2, SpeechOutcome.Completed);
        Assert.False(channel.SpeakingPrivate);

        channel.Present(new Announcement(new Utterance("Ana", Sensitive: true), "Ana", 3));
        channel.Cancel();
        Assert.False(channel.SpeakingPrivate);
    }

    [Fact]
    public void An_interrupted_backup_copy_is_not_taken_for_a_finished_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), "hasi-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            var store = new JsonUserDataStore(root, time);
            store.Write("agenda", "ieri");
            time.Advance(TimeSpan.FromDays(1));
            // A copy cut off by a power failure leaves only the temporary file.
            Directory.CreateDirectory(store.BackupFolder);
            var backup = Path.Combine(store.BackupFolder, "agenda-2026-10-25.json");
            File.WriteAllText(backup + ".tmp", "\"ie");

            store.Write("agenda", "azi");

            Assert.Equal("\"ieri\"", File.ReadAllText(backup).Trim());
            Assert.False(File.Exists(backup + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
