using Hasi.Core.Diagnostics;
using Hasi.Core.Localization;
using Hasi.Core.Output;
using Hasi.Core.Speech;

namespace Hasi.App;

/// <summary>
/// Plays a phrase through each compared engine in turn, each with its own announcement ("Varianta 1, Paul prin
/// SAPI."). It is also an output channel, so any key that silences HASI stops the comparison too.
/// </summary>
internal sealed class EngineVoiceComparison(
    IReadOnlyList<(string Name, ISpeechEngine Engine)> variants,
    ITextNormalizer normalizer,
    ILocalizer localizer,
    IDiagnosticLog log) : IVoiceComparison, IOutputChannel, IDisposable
{
    private CancellationTokenSource _playing = new();

    public IReadOnlyList<string> Variants { get; } = [.. variants.Select(v => v.Name)];

    public void Play(string text, int? variant = null)
    {
        Stop();
        var token = _playing.Token;
        var chosen = variant is { } only ? [variants[only]] : variants;
        _ = PlayAsync(text, chosen, token);
    }

    public void Stop()
    {
        _playing.Cancel();
        _playing.Dispose();
        _playing = new CancellationTokenSource();
        foreach (var (_, engine) in variants) engine.Stop();
    }

    // Any interruption of HASI's own speech stops the comparison as well.
    void IOutputChannel.Cancel() => Stop();

    void IOutputChannel.Present(Announcement announcement) { }

    public void Dispose()
    {
        Stop();
        foreach (var (_, engine) in variants) engine.Dispose();
    }

    private async Task PlayAsync(string text, IReadOnlyList<(string Name, ISpeechEngine Engine)> chosen, CancellationToken token)
    {
        var spoken = normalizer.Normalize(text);
        foreach (var (name, engine) in chosen)
        {
            if (token.IsCancellationRequested) return;
            var number = variants.ToList().FindIndex(v => v.Name == name) + 1;
            var announce = normalizer.Normalize(localizer.Get("compare.announce", ("number", number), ("name", name)));
            log.Write("COMPARE", $"engine={engine.Id} variant={number} text=\"{spoken}\"");
            engine.Speak(announce);
            engine.Speak(spoken);
            await engine.WaitUntilIdleAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(true);
            if (token.IsCancellationRequested) return;
            await Task.Delay(400, CancellationToken.None).ConfigureAwait(true);
        }
    }
}
