using NAudio.Wave;

namespace Hasi.Speech.Audio;

/// <summary>
/// The sound card's view of HASI speech: a queue of synthesized samples that never runs dry. When the
/// queue is empty it plays silence, so the audio stream stays open and Bluetooth headphones never fall
/// asleep (which would cut the first syllable of the next message).
/// Stop fades out over a few milliseconds instead of cutting, so there is no click.
/// Thread-safe: the synthesizer writes from its thread, the audio engine reads from its own.
/// </summary>
public sealed class SpeechSampleSource : ISampleProvider
{
    private readonly Lock _lock = new();
    private readonly Queue<Segment> _segments = new();
    private readonly int _fadeSamples;
    private Segment? _current;
    private int _offset;
    private int _fadeTotal;
    private int _fadeRemaining;
    private int _silenceRemaining;
    private int _lastStartedUtterance = -1;
    private long _queuedSamples;

    /// <param name="fadeOut">Length of the fade when speech is stopped.</param>
    public SpeechSampleSource(int sampleRate, TimeSpan fadeOut)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _fadeSamples = Math.Max(1, (int)(sampleRate * fadeOut.TotalSeconds));
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Linear gain applied to speech, 0 to 1 (the volume setting).</summary>
    public float Gain { get; set; } = 1f;

    /// <summary>Speech waiting to be played.</summary>
    public TimeSpan Buffered =>
        TimeSpan.FromSeconds(Interlocked.Read(ref _queuedSamples) / (double)WaveFormat.SampleRate);

    /// <summary>True while there is speech queued, playing or fading out.</summary>
    public bool IsPlaying
    {
        get
        {
            lock (_lock) return _current is not null || _segments.Count > 0;
        }
    }

    /// <summary>The first sample of an utterance was handed to the sound card (argument: utterance id).</summary>
    public event Action<int>? FirstSoundPlayed;

    /// <summary>The last sample of an utterance was handed to the sound card.</summary>
    public event Action<int>? UtteranceFinished;

    /// <summary>A stop's fade-out has been played completely: from here on there is silence.</summary>
    public event Action? FadeOutCompleted;

    public void Write(int utteranceId, ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty) return;
        var converted = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++) converted[i] = samples[i] / 32768f;
        lock (_lock) _segments.Enqueue(new Segment(utteranceId, converted, End: false));
        Interlocked.Add(ref _queuedSamples, converted.Length);
    }

    /// <summary>Marks the end of an utterance; <see cref="UtteranceFinished"/> fires when it has been played.</summary>
    public void EndUtterance(int utteranceId)
    {
        lock (_lock) _segments.Enqueue(new Segment(utteranceId, [], End: true));
    }

    /// <summary>Silence before the next speech, for example after the audio device was re-opened.</summary>
    public void InsertSilence(TimeSpan duration)
    {
        lock (_lock) _silenceRemaining = (int)(WaveFormat.SampleRate * duration.TotalSeconds);
    }

    /// <summary>Stops speaking: what is playing fades out, everything queued is dropped.</summary>
    public void FadeOutAndClear()
    {
        var nothingPlaying = false;
        lock (_lock)
        {
            _segments.Clear();
            var left = _current is null ? 0 : _current.Samples.Length - _offset;
            if (left > 0 && _fadeRemaining == 0)
            {
                _fadeTotal = _fadeRemaining = Math.Min(_fadeSamples, left);
                Interlocked.Exchange(ref _queuedSamples, left);
            }
            else if (_fadeRemaining == 0)
            {
                _current = null;
                Interlocked.Exchange(ref _queuedSamples, 0);
                nothingPlaying = true;
            }
        }
        if (nothingPlaying) FadeOutCompleted?.Invoke();
    }

    public int Read(Span<float> buffer)
    {
        Action? raise = null;
        lock (_lock)
        {
            var written = 0;
            while (written < buffer.Length)
            {
                if (_fadeRemaining > 0)
                {
                    written += Fade(buffer[written..]);
                    if (_fadeRemaining == 0)
                    {
                        Interlocked.Add(ref _queuedSamples, -(_current!.Samples.Length - _offset));
                        _current = null;
                        raise += FadeOutCompleted;
                    }
                    continue;
                }

                if (_silenceRemaining > 0)
                {
                    var silence = Math.Min(_silenceRemaining, buffer.Length - written);
                    buffer.Slice(written, silence).Clear();
                    written += silence;
                    _silenceRemaining -= silence;
                    continue;
                }

                if (_current is null || _offset >= _current.Samples.Length)
                {
                    if (!_segments.TryDequeue(out var next))
                    {
                        _current = null;
                        break;
                    }
                    if (next.End)
                    {
                        var finished = next.UtteranceId;
                        raise += () => UtteranceFinished?.Invoke(finished);
                        _current = null;
                        continue;
                    }
                    if (next.UtteranceId != _lastStartedUtterance)
                    {
                        _lastStartedUtterance = next.UtteranceId;
                        var started = next.UtteranceId;
                        raise += () => FirstSoundPlayed?.Invoke(started);
                    }
                    _current = next;
                    _offset = 0;
                    continue;
                }

                var count = Math.Min(_current.Samples.Length - _offset, buffer.Length - written);
                var source = _current.Samples.AsSpan(_offset, count);
                var gain = Gain;
                for (var i = 0; i < count; i++) buffer[written + i] = source[i] * gain;
                written += count;
                _offset += count;
                Interlocked.Add(ref _queuedSamples, -count);
            }
            buffer[written..].Clear();
        }
        raise?.Invoke();
        return buffer.Length;
    }

    /// <summary>Plays the rest of the fade into <paramref name="target"/>; returns the samples written.</summary>
    private int Fade(Span<float> target)
    {
        var count = Math.Min(_fadeRemaining, target.Length);
        var source = _current!.Samples.AsSpan(_offset, count);
        for (var i = 0; i < count; i++)
        {
            _fadeRemaining--;
            target[i] = source[i] * Gain * (_fadeRemaining / (float)_fadeTotal);
        }
        _offset += count;
        Interlocked.Add(ref _queuedSamples, -count);
        return count;
    }

    private sealed record Segment(int UtteranceId, float[] Samples, bool End);
}
