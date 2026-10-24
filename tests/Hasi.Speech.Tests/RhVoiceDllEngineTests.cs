using System.Collections.Concurrent;
using Hasi.Core.Output;
using Hasi.Core.Speech;
using Hasi.Speech.Audio;
using Hasi.Speech.RhVoice;

namespace Hasi.Speech.Tests;

public class RhVoiceDllEngineTests
{
    /// <summary>Produces chunks of samples; <see cref="Hold"/> makes it wait between chunks until released.</summary>
    private sealed class FakeSynthesizer : IRhVoiceSynthesizer
    {
        public ConcurrentQueue<(string Text, RhVoiceProsody Prosody)> Calls { get; } = new();

        public ManualResetEventSlim Hold { get; } = new(initialState: true);

        public ManualResetEventSlim Started { get; } = new();

        public int Chunks { get; set; } = 3;

        public bool Throw { get; set; }

        /// <summary>The word RHVoice reports, in UTF-8 bytes (as the real library does).</summary>
        public (int Position, int Length) Word { get; set; } = (0, 5);

        public bool StoppedEarly { get; private set; }

        /// <summary>Reports one more word after a chunk was refused, as RHVoice may while it unwinds after a stop.</summary>
        public bool WordWhileUnwinding { get; set; }

        public string Version => "fake";

        public IReadOnlyList<string> Voices => ["Paul"];

        public void Synthesize(string text, string voiceProfile, RhVoiceProsody prosody, SynthesisCallbacks callbacks)
        {
            Calls.Enqueue((text, prosody));
            Started.Set();
            if (Throw) throw new InvalidOperationException("synthesis failed");
            callbacks.WordStarts?.Invoke(Word.Position, Word.Length);
            for (var i = 0; i < Chunks; i++)
            {
                Hold.Wait(TimeSpan.FromSeconds(5));
                if (!callbacks.Samples(new short[] { 1000, 1000 }))
                {
                    if (WordWhileUnwinding) callbacks.WordStarts?.Invoke(Word.Position, Word.Length);
                    StoppedEarly = true;
                    return;
                }
            }
        }

        public void Dispose() { }
    }

    private sealed class FakeSink : ISpeechAudioSink
    {
        public ConcurrentQueue<string> Log { get; } = new();

        public TimeSpan Buffered => TimeSpan.Zero;

        public bool IsPlaying => false;

        public float Gain { get; set; } = 1;

        public event Action<int>? FirstSoundPlayed;

        public event Action<int>? UtteranceFinished;

        public event Action? FadeOutCompleted;

        public event Action<Exception>? DeviceFailed;

        public void Write(int utteranceId, ReadOnlySpan<short> samples) => Log.Enqueue($"write {utteranceId}");

        public void EndUtterance(int utteranceId) => Log.Enqueue($"end {utteranceId}");

        public void FadeOutAndClear()
        {
            Log.Enqueue("fade");
            FadeOutCompleted?.Invoke();
        }

        public void PlayFirstSound(int id) => FirstSoundPlayed?.Invoke(id);

        public void Finish(int id) => UtteranceFinished?.Invoke(id);

        public void FailDevice() => DeviceFailed?.Invoke(new InvalidOperationException("device gone"));

        public void Dispose() { }
    }

    private static void WaitFor(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) Thread.Sleep(5);
        Assert.True(condition(), "timed out");
    }

    [Fact]
    public void Messages_are_synthesized_in_order_and_ended()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");

        engine.Speak("unu");
        engine.Speak("doi");
        WaitFor(() => sink.Log.Contains("end 2"));

        Assert.Equal(["unu", "doi"], synth.Calls.Select(c => c.Text));
        Assert.Equal(["write 1", "write 1", "write 1", "end 1", "write 2", "write 2", "write 2", "end 2"], sink.Log);
    }

    [Fact]
    public void Stop_interrupts_the_synthesis_drops_the_queue_and_fades_out()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");
        synth.Hold.Reset();

        engine.Speak("un text lung");
        engine.Speak("nu trebuie rostit");
        synth.Started.Wait(TimeSpan.FromSeconds(5));
        engine.Stop();
        synth.Hold.Set();
        WaitFor(() => !engine.IsSpeaking);

        Assert.True(synth.StoppedEarly);
        Assert.Equal(["un text lung"], synth.Calls.Select(c => c.Text));
        Assert.Contains("fade", sink.Log);
        Assert.DoesNotContain("end 1", sink.Log);
    }

    [Fact]
    public void Speech_continues_normally_after_a_stop()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");

        engine.Stop();
        engine.Speak("după oprire");
        WaitFor(() => sink.Log.Contains("end 1"));
    }

    [Fact]
    public void Preload_loads_the_voice_first_without_playing_and_survives_a_stop()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");

        engine.Preload();
        engine.Stop(); // the welcome message interrupts whatever is "playing"
        engine.Speak("Bun venit");
        WaitFor(() => sink.Log.Contains("end 1"));

        Assert.Equal(["Pregătire.", "Bun venit"], synth.Calls.Select(c => c.Text));
        Assert.Equal(["fade", "write 1", "write 1", "write 1", "end 1"], sink.Log);
    }

    [Fact]
    public void A_synthesis_error_is_reported_as_failed()
    {
        var synth = new FakeSynthesizer { Throw = true };
        using var engine = new RhVoiceDllEngine(synth, new FakeSink(), "Paul");
        Exception? failure = null;
        engine.Failed += (_, e) => failure = e;

        engine.Speak("ceva");
        WaitFor(() => failure is not null);
    }

    [Fact]
    public void An_audio_device_failure_is_reported_as_failed()
    {
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(new FakeSynthesizer(), sink, "Paul");
        Exception? failure = null;
        engine.Failed += (_, e) => failure = e;

        sink.FailDevice();

        WaitFor(() => failure is not null);
    }

    [Fact]
    public void Rate_pitch_and_volume_are_mapped_to_rhvoice_and_the_sink()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul") { Settings = new SpeechSettings(Rate: -3, Pitch: 5, Volume: 40) };

        engine.Speak("test");
        WaitFor(() => !synth.Calls.IsEmpty);

        var prosody = synth.Calls.Single().Prosody;
        Assert.Equal(-0.3, prosody.Rate, precision: 6);
        Assert.Equal(0.5, prosody.Pitch, precision: 6);
        Assert.Equal(0.4f, sink.Gain, precision: 6);
    }

    [Fact]
    public void Word_positions_and_timings_are_reported()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");
        var words = new ConcurrentQueue<string>();
        var metrics = new ConcurrentQueue<string>();
        engine.WordStarted += (_, w) => words.Enqueue($"{w.Position}+{w.Length} {w.Word}");
        engine.MetricReported += (_, m) => metrics.Enqueue(m.Kind);

        engine.Speak("Bună ziua");
        WaitFor(() => sink.Log.Contains("end 1"));
        sink.PlayFirstSound(1);
        sink.Finish(1);
        WaitFor(() => metrics.Count == 2 && words.Count == 1);

        Assert.Equal(["0+4 Bună"], words);
        Assert.Equal(["first-sound", "finished"], metrics);
    }

    [Fact]
    public void Word_positions_in_utf8_bytes_are_converted_to_characters()
    {
        // "în casă": î takes two bytes, so "casă" starts at byte 4 but at character 3, and is 5 bytes long.
        var synth = new FakeSynthesizer { Word = (4, 5) };
        var sink = new FakeSink();
        using var engine = new RhVoiceDllEngine(synth, sink, "Paul");
        var words = new ConcurrentQueue<string>();
        engine.WordStarted += (_, w) => words.Enqueue($"{w.Position}+{w.Length} {w.Word}");

        engine.Speak("în casă");
        WaitFor(() => words.Count == 1);

        Assert.Equal(["3+4 casă"], words);
    }

    /// <summary>
    /// An engine created without a synchronization context, so it raises its events at once, in order, on the thread
    /// that reports (the test's context would run posted events later, in any order).
    /// </summary>
    private static RhVoiceDllEngine Direct(FakeSynthesizer synth, FakeSink sink)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new RhVoiceDllEngine(synth, sink, "Paul");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
    private static ConcurrentQueue<SpeechFinished> Reports(RhVoiceDllEngine engine)
    {
        var reports = new ConcurrentQueue<SpeechFinished>();
        engine.Finished += (_, finished) => reports.Enqueue(finished);
        return reports;
    }

    [Fact]
    public void A_message_is_completed_only_when_the_output_has_played_its_end()
    {
        var sink = new FakeSink();
        using var engine = Direct(new FakeSynthesizer(), sink);
        var reports = Reports(engine);

        engine.Speak("unu", 10);
        WaitFor(() => sink.Log.Contains("end 1"));
        Assert.Empty(reports); // synthesized, not heard yet

        sink.Finish(1);

        Assert.Equal([new SpeechFinished(10, SpeechOutcome.Completed)], reports);
    }

    [Fact]
    public void Stop_cancels_the_message_being_synthesized_and_the_queued_ones_once()
    {
        var synth = new FakeSynthesizer();
        var sink = new FakeSink();
        using var engine = Direct(synth, sink);
        var reports = Reports(engine);
        synth.Hold.Reset();
        engine.Speak("un text lung", 11);
        engine.Speak("nu trebuie rostit", 12);
        synth.Started.Wait(TimeSpan.FromSeconds(5));

        engine.Stop();
        synth.Hold.Set();
        WaitFor(() => !engine.IsSpeaking);
        sink.Finish(1); // a late end of playback changes nothing

        Assert.Equal([new SpeechFinished(11, SpeechOutcome.Cancelled), new SpeechFinished(12, SpeechOutcome.Cancelled)], reports);
    }

    [Fact]
    public void Stop_cancels_a_message_already_synthesized_but_not_yet_heard()
    {
        var sink = new FakeSink();
        using var engine = Direct(new FakeSynthesizer(), sink);
        var reports = Reports(engine);
        engine.Speak("unu", 20);
        WaitFor(() => sink.Log.Contains("end 1"));

        engine.Stop();
        sink.Finish(1);

        Assert.Equal([new SpeechFinished(20, SpeechOutcome.Cancelled)], reports);
    }

    [Fact]
    public void Words_carry_the_id_of_their_message_and_none_is_reported_after_a_stop()
    {
        var synth = new FakeSynthesizer { WordWhileUnwinding = true };
        var sink = new FakeSink();
        using var engine = Direct(synth, sink);
        var words = new ConcurrentQueue<(long Id, string Word)>();
        engine.WordStarted += (_, w) => words.Enqueue((w.Id, w.Word));
        synth.Hold.Reset();
        engine.Speak("Scrisoare secretă", 42);
        synth.Started.Wait(TimeSpan.FromSeconds(5));
        WaitFor(() => words.Count == 1);

        engine.Stop();
        synth.Hold.Set();
        WaitFor(() => !engine.IsSpeaking);

        Assert.True(synth.StoppedEarly);
        Assert.Equal([(42L, "Scris")], words);
    }

    [Fact]
    public void A_message_that_cannot_be_synthesized_is_reported_failed_before_the_engine_failure()
    {
        var synth = new FakeSynthesizer { Throw = true };
        using var engine = Direct(synth, new FakeSink());
        var events = new ConcurrentQueue<string>();
        engine.Finished += (_, f) => events.Enqueue($"{f.Id} {f.Outcome}");
        engine.Failed += (_, _) => events.Enqueue("engine failed");

        engine.Speak("ceva", 5);
        WaitFor(() => events.Count == 2);

        Assert.Equal(["5 Failed", "engine failed"], events);
    }

    [Fact]
    public void An_audio_device_failure_fails_every_message_not_heard_yet()
    {
        var sink = new FakeSink();
        using var engine = Direct(new FakeSynthesizer(), sink);
        var reports = Reports(engine);
        engine.Speak("unu", 1);
        engine.Speak("doi", 2);
        WaitFor(() => sink.Log.Contains("end 2"));

        sink.FailDevice();
        sink.Finish(1);

        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Failed), new SpeechFinished(2, SpeechOutcome.Failed)], reports);
    }
}