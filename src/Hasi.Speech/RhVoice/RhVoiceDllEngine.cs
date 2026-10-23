using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Hasi.Core.Speech;
using Hasi.Speech.Audio;

namespace Hasi.Speech.RhVoice;

/// <summary>
/// RHVoice through RHVoice.dll directly (ADR 0004 experiment), with HASI's own audio output.
/// Messages are synthesized one at a time on a dedicated thread; the audio callback returns "stop" as soon
/// as <see cref="Stop"/> is called, and what is already playing fades out in a few milliseconds.
/// Synthesis runs at most a few seconds ahead of playback, so long readings do not fill memory.
/// </summary>
public sealed class RhVoiceDllEngine : ISpeechEngine, ISpeechMetrics
{
    public const string EngineId = "rhvoice-dll";

    /// <summary>How far synthesis may run ahead of what has been played.</summary>
    public static readonly TimeSpan MaxAhead = TimeSpan.FromSeconds(3);

    private readonly IRhVoiceSynthesizer _synthesizer;
    private readonly ISpeechAudioSink _sink;
    private readonly string _profile;
    private readonly SynchronizationContext? _context;
    private readonly BlockingCollection<(int Id, string Text)> _queue = new();
    private readonly Thread _worker;
    private readonly ConcurrentDictionary<int, Stopwatch> _clocks = new();
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
        _sink.DeviceFailed += ex => Post(() => Failed?.Invoke(this, ex));
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

    public event EventHandler<Exception>? Failed;

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

    public void Speak(string text)
    {
        var id = Interlocked.Increment(ref _nextId);
        _clocks[id] = Stopwatch.StartNew();
        _queue.Add((id, text));
    }

    public void Stop()
    {
        if (IsSpeaking) _stopClock.Restart();
        Interlocked.Increment(ref _generation);
        while (_queue.TryTake(out var dropped)) _clocks.TryRemove(dropped.Id, out _);
        _sink.FadeOutAndClear();
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
        foreach (var (id, text) in _queue.GetConsumingEnumerable())
        {
            var generation = _generation;
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
                Post(() => Failed?.Invoke(this, ex));
            }
            finally
            {
                Interlocked.Decrement(ref _synthesizing);
            }
        }
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

    private void OnFinished(int id)
    {
        if (_clocks.TryRemove(id, out var clock)) Report("finished", clock.Elapsed.TotalMilliseconds);
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
