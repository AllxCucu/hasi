using Hasi.Core.Diagnostics;
using Hasi.Core.Documents;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Speech;
using Hasi.Core.Text;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>Findings of the Codex review of 1db1de6 (reading aloud, speech reports, private words, the text cursor).</summary>
public class CodexReviewRound2Tests
{
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

        /// <summary>The handlers subscribed now, as an engine thread holds them while it raises an event.</summary>
        public EventHandler<SpeechFinished>? Handlers => Finished;

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

    // Finding 1: private words can reach the log after cancellation.

    [Fact]
    public void A_private_message_is_known_as_private_by_its_id_even_after_it_was_cancelled()
    {
        var engine = new ManualEngine();
        var channel = new SpeechOutputChannel(engine, new SilentEarcons(), NullDiagnosticLog.Instance);

        channel.Present(new Announcement(new Utterance("Meniu"), "Meniu", 1));
        channel.Present(new Announcement(new Utterance("Scrisoare către Ana", Sensitive: true), "Scrisoare către Ana", 2));
        channel.Cancel();

        Assert.False(channel.SpeakingPrivate);
        Assert.True(channel.IsPrivateMessage(2)); // a late word of message 2 is still masked
        Assert.False(channel.IsPrivateMessage(1));
        Assert.False(channel.IsPrivateMessage(0));
    }

    [Fact]
    public void Old_private_ids_forgotten_to_save_memory_still_count_as_private()
    {
        var channel = new SpeechOutputChannel(new NullSpeechEngine(), new SilentEarcons(), NullDiagnosticLog.Instance);
        var id = 0L;
        channel.Present(new Announcement(new Utterance("public"), "public", ++id));
        for (var i = 0; i <= SpeechOutputChannel.RememberedPrivateMessages; i++)
        {
            channel.Present(new Announcement(new Utterance("secret", Sensitive: true), "secret", ++id));
        }
        channel.Present(new Announcement(new Utterance("public"), "public", ++id));

        Assert.True(channel.IsPrivateMessage(2)); // forgotten: masked anyway
        Assert.True(channel.IsPrivateMessage(1)); // older than a forgotten one: masking too much is safe
        Assert.True(channel.IsPrivateMessage(id - 1));
        Assert.False(channel.IsPrivateMessage(id));
    }

    // Finding 2: a covered reader keeps ownership of what it says in answer to its reports.

    private sealed class ChainReader : IScreen, ISpeechListener
    {
        public List<(long Token, SpeechOutcome Outcome)> Reports { get; } = [];

        public string Title => "Cititor";

        public Utterance Enter(EnterReason reason, IScreenContext context) => "Cititor.";

        public ScreenResult Handle(InputEvent input, IScreenContext context) =>
            input.Command == Command.Activate ? ScreenResult.SayTracked(new TrackedSpeech("unu", 1)) : ScreenResult.Unhandled;

        public Utterance Help(IScreenContext context) => "Ajutor.";

        public ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context)
        {
            Reports.Add((token, outcome));
            return outcome == SpeechOutcome.Completed && token < 3
                ? ScreenResult.SayTracked(new TrackedSpeech(new Utterance($"bucata {token + 1}", UtterancePriority.Queue), token + 1))
                : ScreenResult.Handled;
        }
    }

    [Fact]
    public void A_reader_covered_by_another_screen_still_owns_the_sentences_it_queues_and_reads_to_the_end()
    {
        var reader = new ChainReader();
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(reader));
        h.Press(Key.Enter);
        // A delayed offer covers the reader without interrupting it (like the update offer).
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Actualizare", [new MenuItem("Da")])), UtterancePriority.Queue);

        h.FinishAllSpeech();

        Assert.Equal([1L, 2L, 3L], reader.Reports.Select(r => r.Token));
        Assert.Equal("Actualizare", h.Shell.Current.Title);
    }

    // Finding 3: a failure of the queued sentence must not skip the sentence still being said.

    private static Document Text(params string[] paragraphs) =>
        new([.. paragraphs.Select(p => new DocumentParagraph(p))], DocumentFormat.Text, MadeByHasi: false);

    private static (ShellHarness H, TextReaderScreen Reader) OpenReader(Document document)
    {
        var reader = new TextReaderScreen("Carte", document);
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(reader));
        return (h, reader);
    }

    [Fact]
    public void When_the_queued_sentence_fails_while_the_first_is_being_said_the_cursor_stays_on_the_first()
    {
        var (h, reader) = OpenReader(Text("Unu. Doi. Trei."));
        Assert.Equal(["Unu.", "Doi."], h.Press(Key.Enter));
        var queued = h.PendingSpeech[1].Id;

        h.Shell.SpeechFinished(queued, SpeechOutcome.Failed);

        Assert.False(reader.IsReadingAll);
        Assert.Equal(new ReadingPosition(0, 0), reader.CurrentPosition);
    }

    // Finding 5: an old voice's report in flight must not take a report of the new voice.

    [Fact]
    public void A_report_still_in_flight_from_a_replaced_voice_does_not_take_the_new_voice_report()
    {
        var paul = new ManualEngine();
        var andrei = new ManualEngine();
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        var reports = new List<SpeechFinished>();
        chain.Finished += (_, f) => reports.Add(f);
        chain.Speak("unu", 5);
        var inFlight = paul.Handlers; // the audio thread is about to report message 5 with these handlers

        chain.Restart([new SpeechCandidate("Andrei", () => andrei)]);
        chain.Speak("doi", 5);
        inFlight!.Invoke(paul, new SpeechFinished(5, SpeechOutcome.Completed));

        Assert.Equal([new SpeechFinished(5, SpeechOutcome.Cancelled)], reports); // Andrei's message is still being said

        andrei.Finish(5, SpeechOutcome.Failed);

        Assert.Equal([new SpeechFinished(5, SpeechOutcome.Cancelled), new SpeechFinished(5, SpeechOutcome.Failed)], reports);
    }

    // Finding 6: an edit must never leave the cursor inside a text element.

    [Fact]
    public void Backspace_that_joins_a_letter_and_a_combining_mark_leaves_the_cursor_after_both()
    {
        var buffer = new TextBuffer("a\n̦b", cursor: 2);

        Assert.Equal("\n", buffer.DeletePrevious());

        Assert.Equal("a̦b", buffer.Text);
        Assert.Equal(2, buffer.Cursor);
        Assert.Equal("b", buffer.TextElementAtCursor);
    }

    [Fact]
    public void Delete_that_joins_a_letter_and_a_combining_mark_leaves_the_cursor_before_both()
    {
        var buffer = new TextBuffer("a\n̦b", cursor: 1);

        Assert.Equal("\n", buffer.DeleteNext());

        Assert.Equal(0, buffer.Cursor);
        Assert.Equal("a̦", buffer.TextElementAtCursor);
    }

    [Fact]
    public void A_joiner_typed_between_two_emoji_leaves_the_cursor_after_the_joined_emoji()
    {
        var buffer = new TextBuffer("\U0001F44D\U0001F44D", cursor: 2);

        buffer.Insert('‍');

        Assert.Equal(buffer.Length, buffer.Cursor);
        Assert.True(buffer.Undo());
        Assert.Equal(2, buffer.Cursor);
        Assert.True(buffer.Redo());
        Assert.Equal(buffer.Length, buffer.Cursor);
    }

    [Fact]
    public void A_cursor_given_inside_a_text_element_starts_at_the_element()
    {
        var buffer = new TextBuffer("șa", cursor: 1);

        Assert.Equal(0, buffer.Cursor);
    }

    // Finding 7: read-all from an empty last PDF page must not repeat earlier text.

    [Fact]
    public void Reading_on_from_an_empty_last_page_says_the_document_has_ended()
    {
        var pdf = new Document(
            [
                new DocumentParagraph("Prima pagină.", Page: 1),
                new DocumentParagraph("A doua pagină.", Page: 2),
            ],
            DocumentFormat.Pdf, MadeByHasi: false)
        {
            PageCount = 3,
        };
        var (h, reader) = OpenReader(pdf);
        h.Press(Key.PageDown);

        Assert.Equal(["Pagina 3 din 3. Pagina nu are text."], h.Press(Key.PageDown));
        Assert.Equal("Rândul 2 din 2. Pagina 3 din 3.", reader.Position(h.Shell).Text);
        Assert.Equal(["Sfârșitul documentului."], h.Press(Key.Enter));
    }
}
