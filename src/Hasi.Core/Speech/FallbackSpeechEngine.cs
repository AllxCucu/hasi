using Hasi.Core.Output;

namespace Hasi.Core.Speech;

/// <summary>A voice HASI may use, best first; <see cref="Start"/> returns null if it cannot start.</summary>
/// <param name="Key">Stable identity ("engine:name"), stored in the settings; <paramref name="VoiceName"/> is what is said.</param>
public sealed record SpeechCandidate(string VoiceName, Func<ISpeechEngine?> Start, string? Key = null);

/// <summary>Raised when the fallback chain moves to the next voice.</summary>
public sealed record VoiceSwitchedEventArgs(string FailedVoice, string NewVoice, Exception Reason);

/// <summary>
/// The fallback chain: speaks with the best voice that works and, when it fails while HASI is running,
/// moves to the next one and says so. Only when every voice has failed does HASI fall silent
/// (the screen still shows every message, and the error tone is played).
/// </summary>
/// <remarks>
/// Reports (<see cref="Finished"/>): each message is reported by the engine that spoke it. When that engine fails or is
/// replaced, every message it has not reported yet is reported here, at once: Failed after a failure, Cancelled after
/// <see cref="Restart"/>. Such messages are not spoken again by the next voice (the application repeats the last
/// message itself when the voice changes). Without any voice, messages are reported Failed.
/// </remarks>
public sealed class FallbackSpeechEngine : ISpeechEngine, ISpeechSettingsTarget
{
    private Queue<SpeechCandidate> _remaining;
    private ISpeechEngine _current = new NullSpeechEngine();
    private SpeechSettings? _settings;

    // Ids given to _current and not reported yet, oldest first. The engine reports on any thread, so this is shared;
    // removing an id under the lock decides who reports it (the engine or this chain), so it is reported once.
    private readonly Lock _lock = new();
    private readonly List<long> _pending = [];
    private EventHandler<SpeechFinished>? _forward;

    /// <summary>
    /// Changes at every Attach and Detach (under <see cref="_lock"/>). A report raised by an engine already detached,
    /// even one in flight on its thread while it was detached, carries an old generation and is ignored.
    /// </summary>
    private int _generation;

    private FallbackSpeechEngine(IEnumerable<SpeechCandidate> candidates)
    {
        _remaining = new Queue<SpeechCandidate>(candidates);
    }

    /// <summary>The key of the voice speaking now (see <see cref="SpeechCandidate.Key"/>).</summary>
    public string? CurrentKey { get; private set; }

    /// <summary>
    /// Starts over with another list of candidates (for example after the user chose another voice).
    /// Returns false, and keeps speaking with nothing, only if none of them starts.
    /// </summary>
    public bool Restart(IEnumerable<SpeechCandidate> candidates, Action<string, Exception>? onStartFailure = null)
    {
        _current.Failed -= OnCurrentFailed;
        Detach(SpeechOutcome.Cancelled);
        _current.Stop();
        _current.Dispose();
        _remaining = new Queue<SpeechCandidate>(candidates);
        return Advance(onStartFailure);
    }

    /// <summary>Applies rate, pitch and volume to the current voice and to every voice started later.</summary>
    public void Apply(SpeechSettings settings)
    {
        _settings = settings;
        (_current as ISpeechSettingsTarget)?.Apply(settings);
    }

    /// <summary>Starts the best candidate that works. <see cref="CurrentVoiceName"/> is null if none does.</summary>
    public static FallbackSpeechEngine Start(IEnumerable<SpeechCandidate> candidates, Action<string, Exception>? onStartFailure = null)
    {
        var chain = new FallbackSpeechEngine(candidates);
        chain.Advance(onStartFailure);
        return chain;
    }

    public string Id => _current.Id;

    public string? CurrentVoiceName { get; private set; }

    public bool IsSpeaking => _current.IsSpeaking;

    /// <summary>The active engine moved to another voice because the previous one failed.</summary>
    public event EventHandler<VoiceSwitchedEventArgs>? VoiceSwitched;

    /// <summary>Raised only when no voice is left.</summary>
    public event EventHandler<Exception>? Failed;

    public event EventHandler<SpeechFinished>? Finished;

    public void Speak(string text, long id)
    {
        if (_forward is null)
        {
            // No voice is left: the message cannot be spoken.
            Finished?.Invoke(this, new SpeechFinished(id, SpeechOutcome.Failed));
            return;
        }
        lock (_lock) _pending.Add(id);
        try
        {
            _current.Speak(text, id);
        }
        catch
        {
            Forget(id); // a Speak that throws reports nothing (see ISpeechEngine.Speak); the caller does
            throw;
        }
    }

    public void Stop() => _current.Stop();

    public Task WaitUntilIdleAsync(TimeSpan timeout) => _current.WaitUntilIdleAsync(timeout);

    public void Dispose()
    {
        _current.Failed -= OnCurrentFailed;
        Detach(SpeechOutcome.Cancelled);
        _current.Dispose();
    }

    private void OnCurrentFailed(object? sender, Exception error)
    {
        var failedVoice = CurrentVoiceName ?? _current.Id;
        _current.Failed -= OnCurrentFailed;
        // Before the next voice speaks anything: what the failed voice still owed is reported Failed now.
        Detach(SpeechOutcome.Failed);
        _current.Dispose();
        if (Advance(onStartFailure: null))
        {
            VoiceSwitched?.Invoke(this, new VoiceSwitchedEventArgs(failedVoice, CurrentVoiceName!, error));
        }
        else
        {
            Failed?.Invoke(this, error);
        }
    }

    private bool Advance(Action<string, Exception>? onStartFailure)
    {
        while (_remaining.TryDequeue(out var candidate))
        {
            ISpeechEngine? engine;
            try
            {
                engine = candidate.Start();
            }
#pragma warning disable CA1031 // A voice that cannot start is skipped; the next one is tried.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                onStartFailure?.Invoke(candidate.VoiceName, ex);
                continue;
            }
            if (engine is null) continue;

            _current = engine;
            CurrentVoiceName = candidate.VoiceName;
            CurrentKey = candidate.Key;
            if (_settings is { } settings) (engine as ISpeechSettingsTarget)?.Apply(settings);
            _current.Failed += OnCurrentFailed;
            Attach(engine);
            return true;
        }
        _current = new NullSpeechEngine();
        CurrentVoiceName = null;
        CurrentKey = null;
        return false;
    }

    /// <summary>Forwards the reports of <paramref name="engine"/>, until <see cref="Detach"/>.</summary>
    private void Attach(ISpeechEngine engine)
    {
        int generation;
        lock (_lock) generation = ++_generation;
        _forward = (_, finished) =>
        {
            // A late report from an engine already detached is stale: Detach reported its messages, and the same id
            // may already belong to a message of the next voice. Checked and removed atomically.
            bool reported;
            lock (_lock) reported = generation == _generation && _pending.Remove(finished.Id);
            if (reported) Finished?.Invoke(this, finished);
        };
        engine.Finished += _forward;
    }

    /// <summary>Stops listening to the current engine and reports, with <paramref name="outcome"/>, what it still owed.</summary>
    private void Detach(SpeechOutcome outcome)
    {
        if (_forward is not null) _current.Finished -= _forward;
        _forward = null;
        long[] owed;
        lock (_lock)
        {
            _generation++;
            owed = [.. _pending];
            _pending.Clear();
        }
        foreach (var id in owed) Finished?.Invoke(this, new SpeechFinished(id, outcome));
    }

    private bool Forget(long id)
    {
        lock (_lock) return _pending.Remove(id);
    }
}
