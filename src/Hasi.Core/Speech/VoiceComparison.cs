namespace Hasi.Core.Speech;

/// <summary>
/// Plays the same text through several voices, one after the other, so the tester can compare them by ear
/// (Diagnostic → Comparare voci). Provided by the host only when every compared voice is available.
/// </summary>
public interface IVoiceComparison
{
    /// <summary>What each variant is called, in playing order, for example "Paul prin SAPI".</summary>
    IReadOnlyList<string> Variants { get; }

    /// <summary>Plays <paramref name="text"/> through every variant in turn, or only through <paramref name="variant"/>.</summary>
    void Play(string text, int? variant = null);

    void Stop();
}
