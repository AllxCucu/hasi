using System.Diagnostics;
using System.Speech.Synthesis;
using Hasi.Core.Speech;

namespace Hasi.Speech;

/// <summary>
/// SAPI5 voices through System.Speech. RHVoice "Paul" appears here once its SAPI installer has run
/// (see ADR 0002). Must be created and used on one thread.
/// </summary>
public sealed class Sapi5Engine : ISpeechEngine
{
    private readonly SpeechSynthesizer _synthesizer = new();

    public Sapi5Engine()
    {
        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    public string Id => "sapi5";

    public string? CurrentVoiceName => _synthesizer.Voice?.Name;

    public bool IsSpeaking => _synthesizer.State == SynthesizerState.Speaking;

    /// <summary>SAPI rate, from -10 (slowest) to 10 (fastest).</summary>
    public int Rate
    {
        get => _synthesizer.Rate;
        set => _synthesizer.Rate = Math.Clamp(value, -10, 10);
    }

    public IReadOnlyList<VoiceCandidate> Voices =>
        [.. _synthesizer.GetInstalledVoices()
            .Where(v => v.Enabled)
            .Select(v => new VoiceCandidate(v.VoiceInfo.Name, v.VoiceInfo.Culture?.Name ?? "", "sapi5"))];

    public void SelectVoice(string name) => _synthesizer.SelectVoice(name);

    public void Speak(string text) => _synthesizer.SpeakAsync(text);

    public void Stop() => _synthesizer.SpeakAsyncCancelAll();

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
}
