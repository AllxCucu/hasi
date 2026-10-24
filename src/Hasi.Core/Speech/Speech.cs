using Hasi.Core.Diagnostics;
using Hasi.Core.Output;

namespace Hasi.Core.Speech;

/// <summary>A speech synthesizer. Implementations: RHVoice direct, SAPI5, OneCore, none.</summary>
public interface ISpeechEngine : IDisposable
{
    string Id { get; }

    string? CurrentVoiceName { get; }

    bool IsSpeaking { get; }

    /// <summary>
    /// Queues text and returns immediately. Its end is reported exactly once by <see cref="Finished"/> with
    /// <paramref name="id"/> (0: nobody waits for it, but it is still reported). Ids need not be unique.
    /// If it throws, the message was not queued and is not reported.
    /// </summary>
    void Speak(string text, long id);

    /// <summary>Stops speaking at once and drops queued text; each dropped message is reported Cancelled.</summary>
    void Stop();

    Task WaitUntilIdleAsync(TimeSpan timeout);

    /// <summary>
    /// A message ended: Completed when all of its sound has been played, Cancelled when <see cref="Stop"/> dropped it,
    /// Failed when it could not be spoken. Raised once per message, on any thread.
    /// </summary>
    event EventHandler<SpeechFinished>? Finished;

    /// <summary>A queued message could not be spoken. Raised on the thread that uses the engine.</summary>
    event EventHandler<Exception>? Failed;
}

public static class SpeechEngineExtensions
{
    /// <summary>Speaks text whose end nobody waits for (reported with id 0).</summary>
    public static void Speak(this ISpeechEngine engine, string text)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.Speak(text, 0);
    }
}

public interface IEarconPlayer
{
    void Play(Earcon earcon);
}

/// <summary>
/// Used when no synthesizer works: the screen still shows every message. Each message is reported Completed at once,
/// synchronously, from inside <see cref="Speak"/>.
/// </summary>
public sealed class NullSpeechEngine : ISpeechEngine
{
    public string Id => "none";

    public string? CurrentVoiceName => null;

    public bool IsSpeaking => false;

    public event EventHandler<SpeechFinished>? Finished;

    public void Speak(string text, long id) => Finished?.Invoke(this, new SpeechFinished(id, SpeechOutcome.Completed));

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
public sealed class SpeechOutputChannel : IOutputChannel
{
    private readonly ISpeechEngine _engine;
    private readonly IEarconPlayer _earcons;
    private readonly IDiagnosticLog _log;

    public SpeechOutputChannel(ISpeechEngine engine, IEarconPlayer earcons, IDiagnosticLog log)
    {
        _engine = engine;
        _earcons = earcons;
        _log = log;
        _engine.Finished += (_, finished) =>
        {
            lock (_private) _private.Remove(finished.Id);
            Finished?.Invoke(this, finished);
        };
    }

    /// <summary>How many private announcement ids <see cref="IsPrivateMessage"/> remembers by name.</summary>
    public const int RememberedPrivateMessages = 4096;

    /// <summary>Ids of private announcements (<see cref="Utterance.Sensitive"/>) not yet reported finished.</summary>
    private readonly HashSet<long> _private = [];

    /// <summary>Ids of the latest private announcements, finished or not (guarded by <see cref="_private"/>).</summary>
    private readonly HashSet<long> _privateIds = [];

    /// <summary>The same ids, oldest first, to forget the oldest one.</summary>
    private readonly Queue<long> _privateOrder = new();

    /// <summary>The newest private id forgotten to keep the memory bounded: every id up to it counts as private.</summary>
    private long _forgottenUpTo;

    /// <summary>
    /// True while a private announcement may still be heard. Engine callbacks about words come later than the screen
    /// that said them may have closed, so the host masks them by this, not by the current screens. Any thread.
    /// </summary>
    public bool SpeakingPrivate
    {
        get
        {
            lock (_private) return _private.Count > 0;
        }
    }

    /// <summary>
    /// Whether announcement <paramref name="id"/> was private, even long after it was reported finished: an engine may
    /// still report words of a cancelled message while it unwinds. Ids older than the last
    /// <see cref="RememberedPrivateMessages"/> private ones count as private (masking too much is safe). Any thread.
    /// </summary>
    public bool IsPrivateMessage(long id)
    {
        lock (_private) return id > 0 && (id <= _forgottenUpTo || _privateIds.Contains(id));
    }

    /// <summary>
    /// The end of an announcement (by <see cref="Announcement.Id"/>), as the engine reports it: on any thread, possibly
    /// before <see cref="Present"/> returns. The host marshals it to the shell's thread.
    /// </summary>
    public event EventHandler<SpeechFinished>? Finished;

    public void Present(Announcement announcement)
    {
        var utterance = announcement.Utterance;
        if (utterance.Earcon != Earcon.None) _earcons.Play(utterance.Earcon);
        if (string.IsNullOrWhiteSpace(announcement.SpokenText))
        {
            // Nothing to hear, so nothing to wait for.
            Finished?.Invoke(this, new SpeechFinished(announcement.Id, SpeechOutcome.Completed));
            return;
        }

        if (_log.IsEnabled)
        {
            var text = utterance.Sensitive ? TextDiagnosticLog.Mask : announcement.SpokenText;
            // The id matches the SPEECH line that reports how the message ended.
            _log.Write("SPEAK", $"engine={_engine.Id} text=\"{text}\" id={announcement.Id}");
        }
        if (utterance.Sensitive) RememberPrivate(announcement.Id);
        try
        {
            _engine.Speak(announcement.SpokenText, announcement.Id);
        }
        catch
        {
            // An engine that throws has not queued the message, so it would never report it.
            lock (_private) _private.Remove(announcement.Id);
            Finished?.Invoke(this, new SpeechFinished(announcement.Id, SpeechOutcome.Failed));
            throw;
        }
    }

    public void Cancel()
    {
        if (_engine.IsSpeaking) _log.Write("STOP", $"engine={_engine.Id}");
        _engine.Stop();
    }

    private void RememberPrivate(long id)
    {
        lock (_private)
        {
            _private.Add(id);
            if (!_privateIds.Add(id)) return;
            _privateOrder.Enqueue(id);
            if (_privateOrder.Count <= RememberedPrivateMessages) return;
            var forgotten = _privateOrder.Dequeue();
            _privateIds.Remove(forgotten);
            _forgottenUpTo = Math.Max(_forgottenUpTo, forgotten);
        }
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
