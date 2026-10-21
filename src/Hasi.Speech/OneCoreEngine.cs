using System.Diagnostics;
using Hasi.Core.Speech;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Hasi.Speech;

/// <summary>
/// Windows OneCore voices (for example "Microsoft Andrei", ro-RO), which SAPI5 does not see.
/// Text is synthesized one message at a time and played through a MediaPlayer. Use from the UI thread.
/// </summary>
public sealed class OneCoreEngine : ISpeechEngine
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new() { AudioCategory = MediaPlayerAudioCategory.Speech };
    private readonly Queue<string> _queue = new();
    private TaskCompletionSource? _playback;
    private int _generation;
    private bool _busy;

    public OneCoreEngine()
    {
        _player.MediaEnded += (_, _) => _playback?.TrySetResult();
        _player.MediaFailed += (_, _) => _playback?.TrySetResult();
    }

    public string Id => "onecore";

    public string? CurrentVoiceName => _synthesizer.Voice?.DisplayName;

    public bool IsSpeaking => _busy;

    public static IReadOnlyList<VoiceCandidate> Voices =>
        [.. SpeechSynthesizer.AllVoices.Select(v => new VoiceCandidate(v.DisplayName, v.Language, "onecore"))];

    /// <summary>Same scale as SAPI: -10 (slowest) to 10 (fastest), 0 is normal.</summary>
    public int Rate
    {
        get;
        set
        {
            field = Math.Clamp(value, -10, 10);
            _synthesizer.Options.SpeakingRate = Math.Clamp(Math.Pow(3, field / 10.0), 0.5, 6.0);
        }
    }

    public void SelectVoice(string name) =>
        _synthesizer.Voice = SpeechSynthesizer.AllVoices.First(v => v.DisplayName == name);

    public void Speak(string text)
    {
        _queue.Enqueue(text);
        if (!_busy) _ = PumpAsync();
    }

    public void Stop()
    {
        _generation++;
        _queue.Clear();
        _busy = false;
        _player.Pause();
        _player.Source = null;
        _playback?.TrySetResult();
    }

    public async Task WaitUntilIdleAsync(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (_busy && clock.Elapsed < timeout)
        {
            await Task.Delay(50).ConfigureAwait(true);
        }
    }

    public void Dispose()
    {
        Stop();
        _player.Dispose();
        _synthesizer.Dispose();
    }

    /// <summary>Raised when a message could not be synthesized or played; the engine keeps working.</summary>
    public event EventHandler<Exception>? Failed;

    private async Task PumpAsync()
    {
        _busy = true;
        var generation = _generation;
        try
        {
            while (generation == _generation && _queue.TryDequeue(out var text))
            {
                using var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
                if (generation != _generation) return;

                _playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _player.Play();
                await _playback.Task.ConfigureAwait(true);
            }
        }
#pragma warning disable CA1031 // A failed message must not leave the engine stuck and silent.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _queue.Clear();
            Failed?.Invoke(this, ex);
        }
        finally
        {
            if (generation == _generation) _busy = false;
        }
    }
}
