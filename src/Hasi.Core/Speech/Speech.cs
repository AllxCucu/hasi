using Hasi.Core.Diagnostics;
using Hasi.Core.Output;

namespace Hasi.Core.Speech;

/// <summary>A speech synthesizer. Implementations: SAPI5 now; OneCore, RHVoice direct, screen reader later.</summary>
public interface ISpeechEngine : IDisposable
{
    string Id { get; }

    string? CurrentVoiceName { get; }

    bool IsSpeaking { get; }

    /// <summary>Queues text and returns immediately.</summary>
    void Speak(string text);

    /// <summary>Stops speaking at once and drops queued text.</summary>
    void Stop();

    Task WaitUntilIdleAsync(TimeSpan timeout);

    /// <summary>A queued message could not be spoken. Raised on the thread that uses the engine.</summary>
    event EventHandler<Exception>? Failed;
}

public interface IEarconPlayer
{
    void Play(Earcon earcon);
}

/// <summary>Used when no synthesizer works: the screen still shows every message.</summary>
public sealed class NullSpeechEngine : ISpeechEngine
{
    public string Id => "none";

    public string? CurrentVoiceName => null;

    public bool IsSpeaking => false;

    public void Speak(string text) { }

    public void Stop() { }

    public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

    public event EventHandler<Exception>? Failed
    {
        add { }
        remove { }
    }

    public void Dispose() { }
}

/// <summary>Sends announcements to a speech engine, logging exactly what the engine receives.</summary>
public sealed class SpeechOutputChannel(ISpeechEngine engine, IEarconPlayer earcons, IDiagnosticLog log) : IOutputChannel
{
    public void Present(Announcement announcement)
    {
        var utterance = announcement.Utterance;
        if (utterance.Earcon != Earcon.None) earcons.Play(utterance.Earcon);
        if (string.IsNullOrWhiteSpace(announcement.SpokenText)) return;

        if (log.IsEnabled)
        {
            var text = utterance.Sensitive ? TextDiagnosticLog.Mask : announcement.SpokenText;
            log.Write("SPEAK", $"engine={engine.Id} text=\"{text}\"");
        }
        engine.Speak(announcement.SpokenText);
    }

    public void Cancel()
    {
        if (engine.IsSpeaking) log.Write("STOP", $"engine={engine.Id}");
        engine.Stop();
    }
}

/// <param name="Engine">Id of the engine that provides the voice, for example "sapi5" or "onecore".</param>
public sealed record VoiceCandidate(string Name, string Culture, string Engine = "");

public static class VoiceChooser
{
    /// <summary>The Romanian RHVoice voice HASI prefers by default.</summary>
    public const string DefaultPreferredVoice = "Paul";

    /// <summary>
    /// Picks the preferred voice by name, else a Romanian voice, else the first one; ties go to the
    /// earlier voice in the list. Returns null only when there are no voices at all.
    /// </summary>
    public static VoiceCandidate? Choose(IReadOnlyList<VoiceCandidate> voices, string preferredName) =>
        Rank(voices, preferredName).FirstOrDefault();

    /// <summary>All voices, best first, so the next one can be tried when a voice fails to start.</summary>
    public static IReadOnlyList<VoiceCandidate> Rank(IReadOnlyList<VoiceCandidate> voices, string preferredName) =>
        [.. voices
            .Select((voice, index) => (voice, index))
            .OrderBy(v => Score(v.voice, preferredName))
            .ThenBy(v => v.index)
            .Select(v => v.voice)];

    private static int Score(VoiceCandidate voice, string preferredName) =>
        voice.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase) ? 0
        : voice.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase) ? 1
        : voice.Culture.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? 2
        : 3;

    public static bool IsPreferred(VoiceCandidate voice, string preferredName) =>
        voice.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase);
}
