using System.Diagnostics;
using System.Speech.Synthesis;
using Hasi.Core.Output;
using Hasi.Core.Speech;

namespace Hasi.Speech;

/// <summary>
/// SAPI5 voices through System.Speech. RHVoice "Paul" appears here once its SAPI installer has run
/// (see ADR 0002). Must be created and used on one thread; its events arrive on that thread.
/// </summary>
public sealed class Sapi5Engine : ISpeechEngine, ISpeechMetrics, ISpeechSettingsTarget
{
    public const string EngineId = "sapi5";

    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly Dictionary<Prompt, Stopwatch> _prompts = [];
    private readonly Dictionary<Prompt, long> _ids = [];
    private readonly HashSet<Prompt> _started = [];
    private readonly Stopwatch _stopClock = new();
    private SpeechSettings _settings = new();

    public Sapi5Engine()
    {
        _synthesizer.SetOutputToDefaultAudioDevice();
        _synthesizer.SpeakProgress += (_, e) =>
        {
            // The first word of a prompt is the closest SAPI gets to "the first sound".
            if (_prompts.TryGetValue(e.Prompt, out var clock) && _started.Add(e.Prompt))
            {
                Report("first-sound", clock.Elapsed.TotalMilliseconds);
            }
            Report("word", 0, $"{e.CharacterPosition}+{e.CharacterCount} \"{e.Text}\"");
        };
        _synthesizer.SpeakCompleted += (_, e) =>
        {
            _started.Remove(e.Prompt);
            if (_prompts.Remove(e.Prompt, out var clock) && !e.Cancelled)
            {
                Report("finished", clock.Elapsed.TotalMilliseconds);
            }
            if (e.Cancelled && _stopClock.IsRunning && _prompts.Count == 0)
            {
                _stopClock.Stop();
                Report("stop", _stopClock.Elapsed.TotalMilliseconds, "SpeakCompleted(cancelled) received");
            }
            // SAPI raises SpeakCompleted once per prompt, cancelled ones included; with the default audio device as
            // output it comes after the device has played the prompt's last sound. Reported before Failed, so the
            // fallback chain hears about this message first.
            if (_ids.Remove(e.Prompt, out var id))
            {
                var outcome = e.Cancelled ? SpeechOutcome.Cancelled : e.Error is not null ? SpeechOutcome.Failed : SpeechOutcome.Completed;
                Finished?.Invoke(this, new SpeechFinished(id, outcome));
            }
            if (!e.Cancelled && e.Error is not null) Failed?.Invoke(this, e.Error);
        };
    }

    public event EventHandler<Exception>? Failed;

    /// <summary>Raised on the thread that created the engine (System.Speech posts its events to that thread's context).</summary>
    public event EventHandler<SpeechFinished>? Finished;

    public event EventHandler<SpeechMetric>? MetricReported;

    public string Id => EngineId;

    public string? CurrentVoiceName => _synthesizer.Voice?.Name;

    public bool IsSpeaking => _synthesizer.State == SynthesizerState.Speaking;

    /// <summary>Rate maps directly (-10 to 10); volume 0 to 100; SAPI has no pitch setting, so pitch is ignored.</summary>
    public SpeechSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            _synthesizer.Rate = value.Rate;
            _synthesizer.Volume = value.Volume;
        }
    }

    public IReadOnlyList<VoiceCandidate> Voices =>
        [.. _synthesizer.GetInstalledVoices()
            .Where(v => v.Enabled)
            .Select(v => new VoiceCandidate(v.VoiceInfo.Name, v.VoiceInfo.Culture?.Name ?? "", EngineId))];

    public void SelectVoice(string name) => _synthesizer.SelectVoice(name);

    public void Speak(string text, long id)
    {
        var prompt = _synthesizer.SpeakAsync(text);
        _prompts[prompt] = Stopwatch.StartNew();
        _ids[prompt] = id;
    }

    public void Stop()
    {
        if (_prompts.Count > 0) _stopClock.Restart();
        _synthesizer.SpeakAsyncCancelAll();
    }

    public async Task WaitUntilIdleAsync(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        // A prompt queued a moment ago may not have started yet.
        await Task.Delay(100).ConfigureAwait(true);
        while (_synthesizer.State != SynthesizerState.Ready && clock.Elapsed < timeout)
        {
            await Task.Delay(50).ConfigureAwait(true);
        }
    }

    public void Dispose() => _synthesizer.Dispose();

    private void Report(string kind, double milliseconds, string? detail = null) =>
        MetricReported?.Invoke(this, new SpeechMetric(EngineId, kind, milliseconds, detail));

    public void Apply(SpeechSettings settings) => Settings = settings;
}
