using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Hasi.Core.Output;
using Hasi.Core.Speech;
using Hasi.Speech.Audio;

namespace Hasi.Speech.RhVoice;

/// <summary>
/// RHVoice through RHVoice.dll directly (ADR 0004 experiment), with HASI's own audio output.
/// Messages are synthesized one at a time on a dedicated thread; the audio callback returns "stop" as soon
/// as <see cref="Stop"/> is called, and what is already playing fades out in a few milliseconds.
/// Synthesis runs at most a few seconds ahead of playback, so long readings do not fill memory.
/// </summary>
public sealed class RhVoiceDllEngine : ISpeechEngine, ISpeechMetrics, ISpeechSettingsTarget
{
    public const string EngineId = "rhvoice-dll";

    /// <summary>How far synthesis may run ahead of what has been played.</summary>
    public static readonly TimeSpan MaxAhead = TimeSpan.FromSeconds(3);

    private readonly IRhVoiceSynthesizer _synthesizer;
    private readonly ISpeechAudioSink _sink;
    private readonly string _profile;
    private readonly SynchronizationContext? _context;
    private readonly BlockingCollection<(int Id, string Text, int Generation)> _queue = new();
    private readonly Thread _worker;
    private readonly ConcurrentDictionary<int, Stopwatch> _clocks = new();

    /// <summary>
    /// Caller's id of every message not reported yet, by the engine's own id (the one the sink knows). Reports come
    /// from the UI thread (Stop), the synthesis thread (errors) and the audio thread (end of playback): whoever removes
    /// the entry reports the message, so each is reported exactly once.
    /// </summary>
    private readonly ConcurrentDictionary<int, long> _unreported = new();
    private readonly Stopwatch _stopClock = new();
    private volatile int _generation;
    private volatile int _synthesizing;
    private int _nextId;
    private SpeechSettings _settings = new();

    public RhVoiceDllEngine(IRhVoiceSynthesizer synthesizer, ISpeechAudioSink sink, string voiceProfile)
    {
        _synthesizer = synthesizer;
        _sink = sink;
        _profile = voiceProfile;
        _context = SynchronizationContext.Current;
        _sink.FirstSoundPlayed += OnFirstSound;
        _sink.UtteranceFinished += OnFinished;
        _sink.FadeOutCompleted += OnFadeOutCompleted;
        _sink.DeviceFailed += ex =>
        {
            // Without a device nothing written so far will be played, so it will never end by itself.
            ReportAllEnds(SpeechOutcome.Failed);
            Post(() => Failed?.Invoke(this, ex));
        };
        _worker = new Thread(Work) { IsBackground = true, Name = "HASI RHVoice synthesis" };
        _worker.Start();
    }

    public string Id => EngineId;

    public string? CurrentVoiceName => _profile;

    public bool IsSpeaking => _queue.Count > 0 || _synthesizing > 0 || _sink.IsPlaying;

    public SpeechSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            _sink.Gain = value.Gain;
        }
    }

    public void Apply(SpeechSettings settings) => Settings = settings;

    public event EventHandler<Exception>? Failed;

    /// <summary>
    /// Completed when the output has played the message's last sample; posted to the creating thread's context (if it
    /// had one), like every other event of this engine.
    /// </summary>
    public event EventHandler<SpeechFinished>? Finished;

    public event EventHandler<SpeechMetric>? MetricReported;

    /// <summary>A word starts being synthesized: position and length in the text, and the word itself.</summary>
    public event EventHandler<(int Position, int Length, string Word)>? WordStarted;

    /// <summary>
    /// Loads the voice now, in the background, by synthesizing a word without playing it, so the first real
    /// message (the welcome) does not wait for the voice to load. The synthesis thread waits for it, so the
    /// library is never used by two threads at once; a Stop does not cancel it.
    /// </summary>
    public void Preload()
    {
        var clock = Stopwatch.StartNew();
        _preload = Task.Run(() =>
        {
            _synthesizer.Synthesize("Pregătire.", _profile, default, new SynthesisCallbacks { Samples = _ => false });
            Report("preload", clock.Elapsed.TotalMilliseconds, "voice loaded before the first message");
        });
    }

    private Task? _preload;

    public void Speak(string text, long id)
    {
        var own = Interlocked.Increment(ref _nextId);
        _clocks[own] = Stopwatch.StartNew();
        _unreported[own] = id;
        // The message belongs to the generation it was queued in: a Stop() after this drops it even if the
        // synthesis thread takes it from the queue in the middle of that Stop().
        _queue.Add((own, text, _generation));
    }

    public void Stop()
    {
        if (IsSpeaking) _stopClock.Restart();
        Interlocked.Increment(ref _generation);
        while (_queue.TryTake(out var dropped)) _clocks.TryRemove(dropped.Id, out _);
        _sink.FadeOutAndClear();
        // Queued, being synthesized, or written to the sink but not played yet (its end marker was just cleared).
        ReportAllEnds(SpeechOutcome.Cancelled);
    }

    public async Task WaitUntilIdleAsync(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (IsSpeaking && clock.Elapsed < timeout) await Task.Delay(50).ConfigureAwait(true);
    }

    public void Dispose()
    {
        Stop();
        _queue.CompleteAdding();
        _worker.Join(TimeSpan.FromSeconds(2));
        _sink.Dispose();
        _synthesizer.Dispose();
        _queue.Dispose();
    }

    private void Work()
    {
        foreach (var (id, text, generation) in _queue.GetConsumingEnumerable())
        {
            if (generation != _generation)
            {
                _clocks.TryRemove(id, out _); // stopped before its turn; Stop() has reported it
                continue;
            }
            Interlocked.Increment(ref _synthesizing);
            try
            {
                WaitForPreload();
                var settings = _settings;
                var utf8 = Encoding.UTF8.GetBytes(text);
                var callbacks = new SynthesisCallbacks
                {
                    Samples = samples =>
                    {
                        if (generation != _generation) return false;
                        _sink.Write(id, samples);
                        // Back-pressure: stay at most MaxAhead in front of the sound card.
                        while (_sink.Buffered > MaxAhead && generation == _generation) Thread.Sleep(20);
                        return generation == _generation;
                    },
                    WordStarts = (position, length) => ReportWord(utf8, position, length),
                };
                _synthesizer.Synthesize(text, _profile, new RhVoiceProsody(settings.RhVoiceRate, settings.RhVoicePitch), callbacks);
                if (generation == _generation) _sink.EndUtterance(id);
                else _clocks.TryRemove(id, out _);
            }
#pragma warning disable CA1031 // Reported as Failed; the fallback chain moves to the next voice.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _clocks.TryRemove(id, out _);
                ReportEnd(id, SpeechOutcome.Failed); // before Failed, so the fallback chain hears about this message first
                Post(() => Failed?.Invoke(this, ex));
            }
            finally
            {
                Interlocked.Decrement(ref _synthesizing);
            }
        }
    }

    /// <summary>Reports message <paramref name="id"/> (the engine's own id) unless someone else already has.</summary>
    private void ReportEnd(int id, SpeechOutcome outcome)
    {
        if (!_unreported.TryRemove(id, out var callerId)) return;
        var finished = new SpeechFinished(callerId, outcome);
        Post(() => Finished?.Invoke(this, finished));
    }

    /// <summary>Reports every message not reported yet, oldest first.</summary>
    private void ReportAllEnds(SpeechOutcome outcome)
    {
        foreach (var id in _unreported.Keys.Order()) ReportEnd(id, outcome);
    }

    private void WaitForPreload()
    {
        if (_preload is not { } preload) return;
        try
        {
            preload.Wait();
        }
        catch (AggregateException ex)
        {
            Post(() => Failed?.Invoke(this, ex.InnerException ?? ex));
        }
        _preload = null;
    }

    private void OnFirstSound(int id)
    {
        if (_clocks.TryGetValue(id, out var clock)) Report("first-sound", clock.Elapsed.TotalMilliseconds);
    }

    /// <summary>On the audio thread: the output has played the last sample of message <paramref name="id"/>.</summary>
    private void OnFinished(int id)
    {
        if (_clocks.TryRemove(id, out var clock)) Report("finished", clock.Elapsed.TotalMilliseconds);
        ReportEnd(id, SpeechOutcome.Completed);
    }

    private void OnFadeOutCompleted()
    {
        if (!_stopClock.IsRunning) return;
        _stopClock.Stop();
        Report("stop", _stopClock.Elapsed.TotalMilliseconds, "fade-out played; device buffer adds up to " +
                                                              $"{SpeechAudioOutput.LatencyMilliseconds} ms");
    }

    /// <summary>RHVoice reports positions in UTF-8 bytes of the message; HASI logs them as characters.</summary>
    private void ReportWord(byte[] utf8, int position, int length)
    {
        if (position < 0 || length <= 0 || position + length > utf8.Length) return;
        var start = Encoding.UTF8.GetCharCount(utf8, 0, position);
        var word = Encoding.UTF8.GetString(utf8, position, length);
        Post(() => WordStarted?.Invoke(this, (start, word.Length, word.Trim())));
    }

    private void Report(string kind, double milliseconds, string? detail = null) =>
        Post(() => MetricReported?.Invoke(this, new SpeechMetric(EngineId, kind, milliseconds, detail)));

    private void Post(Action action)
    {
        if (_context is null) action();
        else _context.Post(_ => action(), null);
    }
}
