using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Hasi.Speech.Audio;

/// <summary>Where a synthesizer that produces raw samples sends its speech.</summary>
public interface ISpeechAudioSink : IDisposable
{
    TimeSpan Buffered { get; }

    bool IsPlaying { get; }

    /// <summary>0 to 1.</summary>
    float Gain { get; set; }

    event Action<int>? FirstSoundPlayed;

    event Action<int>? UtteranceFinished;

    event Action? FadeOutCompleted;

    /// <summary>The audio device failed and could not be re-opened.</summary>
    event Action<Exception>? DeviceFailed;

    void Write(int utteranceId, ReadOnlySpan<short> samples);

    void EndUtterance(int utteranceId);

    void FadeOutAndClear();
}

/// <summary>
/// WASAPI output through NAudio (MIT), opened once and kept open while HASI runs:
/// <list type="bullet">
/// <item>it follows the default playback device (Windows stream routing), so unplugging headphones or
/// coming back from sleep moves speech to the new device without HASI doing anything;</item>
/// <item>it plays silence between messages, so Bluetooth headphones stay awake and the first syllable is not cut;</item>
/// <item>if the stream still fails, it is rebuilt once, with a short silence before speech resumes.</item>
/// </list>
/// </summary>
public sealed class SpeechAudioOutput : ISpeechAudioSink
{
    /// <summary>Small buffer, so a stop is heard quickly; large enough not to stutter.</summary>
    public const int LatencyMilliseconds = 40;

    private static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(8);
    private static readonly TimeSpan SilenceAfterReopen = TimeSpan.FromMilliseconds(150);

    private readonly SpeechSampleSource _source;
    private WasapiPlayer _player;
    private bool _disposed;

    private SpeechAudioOutput(SpeechSampleSource source, WasapiPlayer player)
    {
        _source = source;
        _player = player;
        _source.FirstSoundPlayed += id => FirstSoundPlayed?.Invoke(id);
        _source.UtteranceFinished += id => UtteranceFinished?.Invoke(id);
        _source.FadeOutCompleted += () => FadeOutCompleted?.Invoke();
        Attach(player);
    }

    public event Action<int>? FirstSoundPlayed;

    public event Action<int>? UtteranceFinished;

    public event Action? FadeOutCompleted;

    public event Action<Exception>? DeviceFailed;

    /// <summary>Name of the device in use, for the diagnostic log.</summary>
    public string DeviceDescription { get; private set; } = "";

    public TimeSpan Buffered => _source.Buffered;

    public bool IsPlaying => _source.IsPlaying;

    public float Gain
    {
        get => _source.Gain;
        set => _source.Gain = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Opens the default playback device. Blocks briefly; safe to call from the UI thread.</summary>
    public static SpeechAudioOutput Open(int sampleRate)
    {
        var source = new SpeechSampleSource(sampleRate, FadeOut);
        // Activation is asynchronous and must not wait on the UI thread's context.
        var player = Task.Run(() => StartAsync(source)).GetAwaiter().GetResult();
        var output = new SpeechAudioOutput(source, player);
        output.Describe();
        return output;
    }

    public void Write(int utteranceId, ReadOnlySpan<short> samples) => _source.Write(utteranceId, samples);

    public void EndUtterance(int utteranceId) => _source.EndUtterance(utteranceId);

    public void FadeOutAndClear() => _source.FadeOutAndClear();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.PlaybackStopped -= OnPlaybackStopped;
        _player.Dispose();
    }

    private static async Task<WasapiPlayer> StartAsync(SpeechSampleSource source)
    {
        var player = await new WasapiPlayerBuilder()
            .WithDefaultDeviceStreamRouting()
            .WithLatency(LatencyMilliseconds)
            .WithCategory(AudioStreamCategory.Speech)
            .BuildAsync()
            .ConfigureAwait(false);

        // Shared mode adapts bit depth and channels but not the sample rate: resample to the device's rate.
        ISampleProvider signal = source;
        var deviceRate = player.DeviceMixFormat?.SampleRate ?? source.WaveFormat.SampleRate;
        if (deviceRate != source.WaveFormat.SampleRate) signal = new WdlResamplingSampleProvider(signal, deviceRate);
        // Speech is mono; on stereo or surround devices send it to both front speakers, not only the centre.
        if ((player.DeviceMixFormat?.Channels ?? 1) >= 2) signal = new MonoToStereoSampleProvider(signal);
        player.Init(new SampleToWaveProvider(signal));
        player.Play();
        return player;
    }

    private void Attach(WasapiPlayer player) => player.PlaybackStopped += OnPlaybackStopped;

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed) return;
        // The source never ends, so any stop is a device problem: re-open once, then give up.
        try
        {
            _player.PlaybackStopped -= OnPlaybackStopped;
            _player.Dispose();
            _player = Task.Run(() => StartAsync(_source)).GetAwaiter().GetResult();
            Attach(_player);
            _source.InsertSilence(SilenceAfterReopen);
            Describe();
        }
#pragma warning disable CA1031 // Reported to the engine, which moves to the next voice.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            DeviceFailed?.Invoke(e.Exception is null ? ex : new AggregateException(e.Exception, ex));
        }
    }

    private void Describe()
    {
        var format = _player.DeviceMixFormat;
        DeviceDescription = $"default device (stream routing), mix {format?.SampleRate} Hz/{format?.Channels} ch, " +
                            $"latency {_player.LatencyMilliseconds} ms, source {_source.WaveFormat.SampleRate} Hz";
    }
}
