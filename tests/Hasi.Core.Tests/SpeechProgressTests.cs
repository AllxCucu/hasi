using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Speech;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>Speech progress: announcement ids, the speech channel's reports, and their way to the screen that asked.</summary>
public class SpeechProgressTests
{
    /// <summary>Enter reads <see cref="Chunks"/> as tracked messages (token = chunk number); every report is recorded.</summary>
    private class ReaderScreen : IScreen, ISpeechListener
    {
        public List<string> Chunks { get; init; } = ["unu", "doi"];

        public List<(long Token, SpeechOutcome Outcome)> Reports { get; } = [];

        /// <summary>What the screen answers to a report; nothing by default.</summary>
        public Func<long, SpeechOutcome, ScreenResult> OnReport { get; set; } = (_, _) => ScreenResult.Handled;

        public string Title => "Cititor";

        public Utterance Enter(EnterReason reason, IScreenContext context) => "Cititor.";

        public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
        {
            Command.Activate => ScreenResult.SayTracked([.. Chunks.Select((text, i) => new TrackedSpeech(text, i + 1))]),
            Command.Next => ScreenResult.Handled,
            _ => ScreenResult.Unhandled,
        };

        public Utterance Help(IScreenContext context) => "Ajutor.";

        public ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context)
        {
            Reports.Add((token, outcome));
            return OnReport(token, outcome);
        }
    }

    private sealed class PrivateReaderScreen : ReaderScreen, ISensitiveScreen;

    private static (ShellHarness Harness, ReaderScreen Reader) OpenReader(ReaderScreen? reader = null, ShellHarness? harness = null)
    {
        reader ??= new ReaderScreen();
        var h = (harness ?? new ShellHarness()).Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(reader));
        return (h, reader);
    }

    [Fact]
    public void Tracked_messages_are_said_in_order_the_first_with_its_priority_the_others_queued()
    {
        var (h, _) = OpenReader();

        Assert.Equal(["unu", "doi"], h.Press(Key.Enter));

        var said = h.Output.Announcements.TakeLast(2).Select(a => a.Utterance.Priority);
        Assert.Equal([UtterancePriority.Interrupt, UtterancePriority.Queue], said);
        Assert.Equal(["unu", "doi"], h.PendingSpeech.Select(a => a.Utterance.Text));
    }

    [Fact]
    public void Each_message_is_reported_to_its_screen_with_its_token_when_heard()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);

        h.FinishSpeech();
        Assert.Equal([(1L, SpeechOutcome.Completed)], reader.Reports);

        h.FinishSpeech();
        Assert.Equal([(1L, SpeechOutcome.Completed), (2L, SpeechOutcome.Completed)], reader.Reports);
    }

    [Fact]
    public void A_key_cancels_what_is_being_read_and_the_screen_hears_it()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);

        h.Press(Key.Down);

        Assert.Equal([(1L, SpeechOutcome.Cancelled), (2L, SpeechOutcome.Cancelled)], reader.Reports);
        Assert.Empty(h.PendingSpeech);
    }

    [Fact]
    public void A_failed_message_is_reported_as_failed()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);

        h.FinishSpeech(SpeechOutcome.Failed);

        Assert.Equal([(1L, SpeechOutcome.Failed)], reader.Reports);
    }

    [Fact]
    public void A_screen_that_was_closed_hears_nothing()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);

        h.Press(Key.Escape); // cancels both messages, then closes the reader before the reports arrive

        Assert.Empty(reader.Reports);
        Assert.Equal("Fructe", h.Shell.Current.Title);
    }

    [Fact]
    public void A_screen_covered_by_another_one_is_still_open_and_still_hears_its_reports()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Deasupra", [new MenuItem("a")])), UtterancePriority.Queue);

        h.FinishSpeech();

        Assert.Equal([(1L, SpeechOutcome.Completed)], reader.Reports);
    }

    [Fact]
    public void Unknown_ids_and_ids_already_reported_are_ignored()
    {
        var (h, reader) = OpenReader();
        h.Press(Key.Enter);
        var first = h.PendingSpeech[0].Id;
        h.FinishSpeech();

        h.Shell.SpeechFinished(first, SpeechOutcome.Completed);
        h.Shell.SpeechFinished(424242, SpeechOutcome.Completed);
        h.Shell.SpeechFinished(0, SpeechOutcome.Cancelled);

        Assert.Equal([(1L, SpeechOutcome.Completed)], reader.Reports);
    }

    [Fact]
    public void Messages_said_by_a_screen_that_does_not_listen_are_not_tracked()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        h.Shell.Show(ScreenResult.SayTracked(new TrackedSpeech("unu", 1), new TrackedSpeech("doi", 2)));
        h.FinishAllSpeech();

        Assert.Equal("doi", h.LastSaid);
    }

    [Fact]
    public void The_screen_answer_to_a_report_is_applied_so_it_can_read_on_until_the_end()
    {
        var reader = new ReaderScreen { Chunks = ["Primul paragraf."] };
        string[] document = ["Primul paragraf.", "Al doilea.", "Al treilea."];
        reader.OnReport = (token, outcome) => outcome == SpeechOutcome.Completed && token < document.Length
            ? ScreenResult.SayTracked(new TrackedSpeech(new Utterance(document[token], UtterancePriority.Queue), token + 1))
            : ScreenResult.Handled;
        var (h, _) = OpenReader(reader);
        h.Press(Key.Enter);

        Assert.Equal(["Al doilea.", "Al treilea."], h.FinishAllSpeech());
        Assert.Equal([1L, 2L, 3L], reader.Reports.Select(r => r.Token));
        Assert.Empty(h.PendingSpeech);
    }

    [Fact]
    public void A_report_the_screen_does_not_use_says_nothing()
    {
        var (h, reader) = OpenReader();
        reader.OnReport = (_, _) => ScreenResult.Unhandled;
        h.Press(Key.Enter);

        Assert.Empty(h.FinishSpeech());
    }

    [Fact]
    public void Shown_with_interrupt_priority_the_later_messages_still_wait_for_the_first()
    {
        var (h, _) = OpenReader();

        h.Shell.Show(ScreenResult.SayTracked(new TrackedSpeech("unu", 1), new TrackedSpeech("doi", 2)));

        var said = h.Output.Announcements.TakeLast(2).Select(a => a.Utterance.Priority);
        Assert.Equal([UtterancePriority.Interrupt, UtterancePriority.Queue], said);
        Assert.Equal(2, h.PendingSpeech.Count);
    }

    [Fact]
    public void What_a_private_screen_says_after_a_report_stays_out_of_the_log()
    {
        var log = new StringWriter();
        var harness = new ShellHarness(diagnosticLog: new TextDiagnosticLog(log, TimeProvider.System));
        var reader = new PrivateReaderScreen { Chunks = ["Scrisoare secretă"] };
        reader.OnReport = (_, _) => ScreenResult.Say("Continuare secretă");
        var (h, _) = OpenReader(reader, harness);
        h.Press(Key.Enter);

        h.FinishSpeech();

        Assert.Equal("Continuare secretă", h.LastSaid);
        Assert.DoesNotContain("secret", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_announcer_gives_every_announcement_a_new_increasing_id()
    {
        var output = new RecordingChannel();
        var announcer = new Announcer(new OutputHub([output], NullDiagnosticLog.Instance), new PassThroughNormalizer(), NullDiagnosticLog.Instance);

        var first = announcer.Say("unu");
        var second = announcer.Say(new Utterance("doi", UtterancePriority.Queue));
        announcer.Repeat();

        Assert.Equal([first, second], output.Announcements.Take(2).Select(a => a.Id));
        Assert.True(first > 0);
        Assert.True(second > first);
        Assert.True(output.Announcements[2].Id > second);
    }

    [Fact]
    public void An_announcement_with_nothing_to_say_is_heard_at_once()
    {
        var (h, reader) = OpenReader(new ReaderScreen { Chunks = [" "] });

        h.Press(Key.Enter);

        Assert.Equal([(1L, SpeechOutcome.Completed)], reader.Reports);
    }
}

public class SpeechChannelProgressTests
{
    private sealed class FakeEngine : ISpeechEngine
    {
        public List<(string Text, long Id)> Spoken { get; } = [];

        public bool Throw { get; set; }

        public string Id => "fake";

        public string? CurrentVoiceName => "Fake";

        public bool IsSpeaking => false;

        public event EventHandler<SpeechFinished>? Finished;

        public event EventHandler<Exception>? Failed
        {
            add { }
            remove { }
        }

        public void Speak(string text, long id)
        {
            if (Throw) throw new InvalidOperationException("engine broken");
            Spoken.Add((text, id));
        }

        public void Report(long id, SpeechOutcome outcome) => Finished?.Invoke(this, new SpeechFinished(id, outcome));

        public void Stop() { }

        public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

        public void Dispose() { }
    }

    private sealed class NoEarcons : IEarconPlayer
    {
        public void Play(Earcon earcon) { }
    }

    private static (SpeechOutputChannel Channel, List<SpeechFinished> Reports) Channel(ISpeechEngine engine, IDiagnosticLog? log = null)
    {
        var channel = new SpeechOutputChannel(engine, new NoEarcons(), log ?? NullDiagnosticLog.Instance);
        var reports = new List<SpeechFinished>();
        channel.Finished += (_, finished) => reports.Add(finished);
        return (channel, reports);
    }

    [Fact]
    public void The_channel_gives_the_engine_the_announcement_id_and_passes_its_report_on()
    {
        var engine = new FakeEngine();
        var writer = new StringWriter();
        var (channel, reports) = Channel(engine, new TextDiagnosticLog(writer, TimeProvider.System));

        channel.Present(new Announcement(new Utterance("Bună"), "Bună", 7));
        engine.Report(7, SpeechOutcome.Completed);

        Assert.Equal([("Bună", 7L)], engine.Spoken);
        Assert.Equal([new SpeechFinished(7, SpeechOutcome.Completed)], reports);
        Assert.Contains("id=7", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_to_say_is_reported_completed_without_bothering_the_engine()
    {
        var engine = new FakeEngine();
        var (channel, reports) = Channel(engine);

        channel.Present(new Announcement(new Utterance(""), "  ", 3));

        Assert.Empty(engine.Spoken);
        Assert.Equal([new SpeechFinished(3, SpeechOutcome.Completed)], reports);
    }

    [Fact]
    public void An_engine_that_throws_gets_its_message_reported_failed()
    {
        var engine = new FakeEngine { Throw = true };
        var (channel, reports) = Channel(engine);

        Assert.Throws<InvalidOperationException>(() => channel.Present(new Announcement(new Utterance("x"), "x", 4)));

        Assert.Equal([new SpeechFinished(4, SpeechOutcome.Failed)], reports);
    }

    [Fact]
    public void The_null_engine_reports_every_message_completed_at_once()
    {
        using var engine = new NullSpeechEngine();
        var reports = new List<SpeechFinished>();
        engine.Finished += (_, finished) => reports.Add(finished);

        engine.Speak("unu", 1);
        engine.Speak("doi");

        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Completed), new SpeechFinished(0, SpeechOutcome.Completed)], reports);
    }
}
