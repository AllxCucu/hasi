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

/// <summary>When to close an idle audio stream (pure logic, unit tested).</summary>
public static class IdlePolicy
{
    /// <param name="idleTimeout">Zero or negative: never close.</param>
    public static bool ShouldClose(bool isOpen, bool isPlaying, TimeSpan sinceLastSpeech, TimeSpan idleTimeout) =>
        isOpen && !isPlaying && idleTimeout > TimeSpan.Zero && sinceLastSpeech >= idleTimeout;
}

/// <summary>
/// WASAPI output through NAudio (MIT):
/// <list type="bullet">
/// <item>it follows the default playback device (Windows stream routing), so unplugging headphones or
/// coming back from sleep moves speech to the new device without HASI doing anything;</item>
/// <item>between messages it plays silence, so Bluetooth headphones stay awake and the first syllable is not cut;</item>
/// <item>after a configurable time without speech (default one minute) it closes the stream, so the computer
/// can go to sleep and batteries are spared; the next message re-opens it after 150 ms of silence;</item>
/// <item>if the stream fails, it is rebuilt once, with the same short silence before speech resumes.</item>
/// </list>
/// </summary>
public sealed class SpeechAudioOutput : ISpeechAudioSink
{
    /// <summary>Small buffer, so a stop is heard quickly; large enough not to stutter.</summary>
    public const int LatencyMilliseconds = 40;

    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(8);
    private static readonly TimeSpan SilenceAfterReopen = TimeSpan.FromMilliseconds(150);

    private readonly SpeechSampleSource _source;
    private readonly TimeSpan _idleTimeout;
    private readonly Lock _lock = new();
    private readonly Timer _idleTimer;
    private WasapiPlayer? _player;
    private long _lastSpeechTicks = Environment.TickCount64;
    private bool _disposed;

    private SpeechAudioOutput(SpeechSampleSource source, WasapiPlayer player, TimeSpan idleTimeout)
    {
        _source = source;
        _player = player;
        _idleTimeout = idleTimeout;
        _source.FirstSoundPlayed += id => FirstSoundPlayed?.Invoke(id);
        _source.UtteranceFinished += id =>
        {
            Touch();
            UtteranceFinished?.Invoke(id);
        };
        _source.FadeOutCompleted += () => FadeOutCompleted?.Invoke();
        player.PlaybackStopped += OnPlaybackStopped;
        _idleTimer = new Timer(_ => CloseIfIdle(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public event Action<int>? FirstSoundPlayed;

    public event Action<int>? UtteranceFinished;

    public event Action? FadeOutCompleted;

    public event Action<Exception>? DeviceFailed;

    /// <summary>The stream was closed or re-opened; the text is for the diagnostic log.</summary>
    public event Action<string>? StateChanged;

    /// <summary>Name of the device in use, for the diagnostic log.</summary>
    public string DeviceDescription { get; private set; } = "";

    public bool IsOpen
    {
        get
        {
            lock (_lock) return _player is not null;
        }
    }

    public TimeSpan Buffered => _source.Buffered;

    public bool IsPlaying => _source.IsPlaying;

    public float Gain
    {
        get => _source.Gain;
        set => _source.Gain = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Opens the default playback device. Blocks briefly; safe to call from the UI thread.</summary>
    /// <param name="idleTimeout">Close the stream after this long without speech; zero keeps it open.</param>
    public static SpeechAudioOutput Open(int sampleRate, TimeSpan idleTimeout)
    {
        var source = new SpeechSampleSource(sampleRate, FadeOut);
        var player = StartBlocking(source);
        var output = new SpeechAudioOutput(source, player, idleTimeout);
        output.Describe(player);
        return output;
    }

    public void Write(int utteranceId, ReadOnlySpan<short> samples)
    {
        Touch(); // first, so the idle timer cannot close the stream we are about to use
        EnsureOpen();
        _source.Write(utteranceId, samples);
    }

    public void EndUtterance(int utteranceId) => _source.EndUtterance(utteranceId);

    public void FadeOutAndClear() => _source.FadeOutAndClear();

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _idleTimer.Dispose();
        Close();
    }

    private void Touch() => Interlocked.Exchange(ref _lastSpeechTicks, Environment.TickCount64);

    private void CloseIfIdle()
    {
        var since = TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastSpeechTicks));
        if (!IdlePolicy.ShouldClose(IsOpen, _source.IsPlaying, since, _idleTimeout)) return;
        Close();
        StateChanged?.Invoke($"stream closed after {since.TotalSeconds:F0} s without speech");
    }

    private void Close()
    {
        WasapiPlayer? player;
        lock (_lock)
        {
            player = _player;
            _player = null;
        }
        if (player is null) return;
        player.PlaybackStopped -= OnPlaybackStopped;
        player.Dispose();
    }

    /// <summary>Re-opens the stream for the next message, with a short silence so nothing is cut.</summary>
    private void EnsureOpen()
    {
        lock (_lock)
        {
            if (_player is not null || _disposed) return;
            try
            {
                _player = StartBlocking(_source);
                _player.PlaybackStopped += OnPlaybackStopped;
                _source.InsertSilence(SilenceAfterReopen);
                Describe(_player);
            }
#pragma warning disable CA1031 // Reported to the engine, which moves to the next voice.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                DeviceFailed?.Invoke(ex);
                return;
            }
        }
        StateChanged?.Invoke($"stream re-opened with {SilenceAfterReopen.TotalMilliseconds:F0} ms of silence; {DeviceDescription}");
    }

    private static WasapiPlayer StartBlocking(SpeechSampleSource source) =>
        // Activation is asynchronous and must not wait on the UI thread's context.
        Task.Run(() => StartAsync(source)).GetAwaiter().GetResult();

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

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // The source never ends, so a stop we did not ask for is a device problem: re-open once, then give up.
        Close();
        lock (_lock)
        {
            if (_disposed) return;
        }
        EnsureOpen(); // reports DeviceFailed itself if the device cannot be opened again
    }

    private void Describe(WasapiPlayer player)
    {
        var format = player.DeviceMixFormat;
        DeviceDescription = $"default device (stream routing), mix {format?.SampleRate} Hz/{format?.Channels} ch, " +
                            $"latency {player.LatencyMilliseconds} ms, source {_source.WaveFormat.SampleRate} Hz, " +
                            $"closes after {(_idleTimeout > TimeSpan.Zero ? $"{_idleTimeout.TotalSeconds:F0} s" : "never")} without speech";
    }
}
