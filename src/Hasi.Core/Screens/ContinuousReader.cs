using Hasi.Core.Output;
using Hasi.Core.Text;

namespace Hasi.Core.Screens;

/// <summary>
/// "Citește tot": reads sentences one after the other, for a screen that is an <see cref="ISpeechListener"/> (the
/// document reader, the editor). At most two sentences wait in the speech queue, so the voice never pauses between
/// them and a stop loses little. When a sentence has been heard, the cursor goes to the next one and one more is
/// queued. Stopped by a key, Ctrl or a failure, it leaves the cursor at the start of the sentence that was being said,
/// so reading on repeats that sentence from its start.
/// </summary>
/// <param name="moveTo">Puts the screen's cursor at a position of its text.</param>
/// <param name="dress">Turns what is said into the screen's own utterance (private, with the text shown).</param>
public sealed class ContinuousReader(Action<int> moveTo, Func<Utterance, Utterance> dress)
{
    /// <summary>How many sentences are given to speech ahead: the one being said and the next one.</summary>
    public const int Ahead = 2;

    private readonly Action<int> _moveTo = moveTo ?? throw new ArgumentNullException(nameof(moveTo));
    private readonly Func<Utterance, Utterance> _dress = dress ?? throw new ArgumentNullException(nameof(dress));
    private readonly List<(ReadingSentence Sentence, int Index)> _queued = [];
    private IEnumerator<ReadingSentence>? _sentences;
    private ReadingSentence? _last;

    /// <summary>Changes at every start and stop: reports about sentences of an earlier reading are ignored.</summary>
    private long _generation;

    private int _nextIndex;

    public bool IsReading => _sentences is not null;

    /// <summary>
    /// Starts reading <paramref name="sentences"/> (the first one is where the cursor goes); with none, says
    /// "Sfârșitul documentului." with the edge sound.
    /// </summary>
    public ScreenResult Start(IEnumerable<ReadingSentence> sentences, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(context);
        Stop();
        _generation++;
        _sentences = sentences.GetEnumerator();
        _nextIndex = 0;
        _last = null;
        var items = new List<TrackedSpeech>();
        while (items.Count < Ahead && Queue() is { } item) items.Add(item);
        if (items.Count == 0)
        {
            Finish();
            return ScreenResult.Say(_dress(new Utterance(context.Localizer.Get("edit.documentEnd"), Earcon: Earcon.EdgeOfList)));
        }
        _moveTo(_queued[0].Sentence.Start);
        return ScreenResult.SayTracked([.. items]);
    }

    /// <summary>
    /// Stops reading, if it was: the cursor goes to the start of the sentence being said. Returns whether it was reading.
    /// </summary>
    public bool Stop() => Stop(0);

    /// <summary>What the screen answers to a report of its tracked speech.</summary>
    public ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsReading || token >> 32 != _generation) return ScreenResult.Handled;
        var index = (int)(token & uint.MaxValue);
        var position = _queued.FindIndex(q => q.Index == index);
        if (position < 0) return ScreenResult.Handled;

        switch (outcome)
        {
            case SpeechOutcome.Cancelled:
                Stop(position);
                return ScreenResult.Handled;
            case SpeechOutcome.Failed:
                Stop(position);
                return ScreenResult.Say(_dress(new Utterance(context.Localizer.Get("reader.readAll.failed"), Earcon: Earcon.Error)));
        }

        _queued.RemoveRange(0, position + 1);
        var next = Queue();
        if (_queued.Count > 0)
        {
            _moveTo(_queued[0].Sentence.Start);
            return next is null ? ScreenResult.Handled : ScreenResult.SayTracked(next);
        }

        // The last sentence has been heard.
        var end = _last?.End;
        Finish();
        if (end is { } e) _moveTo(e);
        return ScreenResult.Say(_dress(new Utterance(context.Localizer.Get("edit.documentEnd"), UtterancePriority.Queue)));
    }

    /// <summary>Takes the next sentence into the queue, as a tracked message that waits for the one before it.</summary>
    private TrackedSpeech? Queue()
    {
        if (_sentences is null || !_sentences.MoveNext()) return null;
        var sentence = _sentences.Current;
        var index = _nextIndex++;
        _queued.Add((sentence, index));
        _last = sentence;
        var priority = _queued.Count == 1 && index == 0 ? UtterancePriority.Interrupt : UtterancePriority.Queue;
        return new TrackedSpeech(_dress(new Utterance(sentence.Text, priority)), (_generation << 32) | (uint)index);
    }

    private bool Stop(int position)
    {
        if (!IsReading) return false;
        var start = position < _queued.Count ? _queued[position].Sentence.Start : (int?)null;
        Finish();
        if (start is { } s) _moveTo(s);
        return true;
    }

    private void Finish()
    {
        _generation++;
        _sentences?.Dispose();
        _sentences = null;
        _queued.Clear();
    }
}
