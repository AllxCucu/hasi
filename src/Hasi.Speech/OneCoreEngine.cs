using System.Diagnostics;
using Hasi.Core.Speech;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Hasi.Speech;

/// <summary>
/// Windows OneCore voices (for example "Microsoft Andrei", ro-RO). Text is synthesized one message at
/// a time; each message gets its own MediaPlayer, so a late event from an old message can never end
/// a newer one. Use from the UI thread.
/// </summary>
public sealed class OneCoreEngine : ISpeechEngine, ISpeechSettingsTarget
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly Queue<string> _queue = new();
    private Playback? _current;
    private int _generation;
    private bool _busy;

    public string Id => "onecore";

    public string? CurrentVoiceName => _synthesizer.Voice?.DisplayName;

    public bool IsSpeaking => _busy;

    public static IReadOnlyList<VoiceCandidate> Voices =>
        [.. SpeechSynthesizer.AllVoices.Select(v => new VoiceCandidate(v.DisplayName, v.Language, "onecore"))];

    /// <summary>HASI's rate, pitch and volume, mapped to OneCore's ranges.</summary>
    public SpeechSettings Settings
    {
        get;
        set
        {
            field = value;
            _synthesizer.Options.SpeakingRate = value.OneCoreRate;
            _synthesizer.Options.AudioPitch = value.OneCorePitch;
            _synthesizer.Options.AudioVolume = value.Gain;
        }
    } = new();

    /// <summary>A message could not be synthesized or played; the engine keeps working.</summary>
    public event EventHandler<Exception>? Failed;

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
        _current?.Cancel();
        _current = null;
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
        _synthesizer.Dispose();
    }

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

                using var playback = new Playback(MediaSource.CreateFromStream(stream, stream.ContentType));
                _current = playback;
                await playback.Completion.ConfigureAwait(true);
                if (ReferenceEquals(_current, playback)) _current = null;
            }
        }
#pragma warning disable CA1031 // A failed message must not leave the engine stuck and silent.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Only the pump that still owns the queue may clear it; an obsolete one must not
            // delete messages queued after a Stop().
            if (generation == _generation) _queue.Clear();
            Failed?.Invoke(this, ex);
        }
        finally
        {
            if (generation == _generation) _busy = false;
        }
    }

    /// <summary>One message on its own player: its events can only complete its own task.</summary>
    private sealed class Playback : IDisposable
    {
        private readonly MediaPlayer _player = new() { AudioCategory = MediaPlayerAudioCategory.Speech };
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Playback(MediaSource source)
        {
            _player.MediaEnded += (_, _) => _completion.TrySetResult();
            _player.MediaFailed += (_, e) => _completion.TrySetException(
                new InvalidOperationException($"OneCore playback failed: {e.Error} {e.ErrorMessage}", e.ExtendedErrorCode));
            _player.Source = source;
            _player.Play();
        }

        public Task Completion => _completion.Task;

        public void Cancel()
        {
            _player.Pause();
            _completion.TrySetResult();
        }

        public void Dispose() => _player.Dispose();
    }

    public void Apply(SpeechSettings settings) => Settings = settings;
}
