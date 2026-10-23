namespace Hasi.Core.Speech;

/// <summary>A voice HASI may use, best first; <see cref="Start"/> returns null if it cannot start.</summary>
public sealed record SpeechCandidate(string VoiceName, Func<ISpeechEngine?> Start);

/// <summary>Raised when the fallback chain moves to the next voice.</summary>
public sealed record VoiceSwitchedEventArgs(string FailedVoice, string NewVoice, Exception Reason);

/// <summary>
/// The fallback chain: speaks with the best voice that works and, when it fails while HASI is running,
/// moves to the next one and says so. Only when every voice has failed does HASI fall silent
/// (the screen still shows every message, and the error tone is played).
/// </summary>
public sealed class FallbackSpeechEngine : ISpeechEngine
{
    private readonly Queue<SpeechCandidate> _remaining;
    private ISpeechEngine _current = new NullSpeechEngine();

    private FallbackSpeechEngine(IEnumerable<SpeechCandidate> candidates)
    {
        _remaining = new Queue<SpeechCandidate>(candidates);
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

    public void Speak(string text) => _current.Speak(text);

    public void Stop() => _current.Stop();

    public Task WaitUntilIdleAsync(TimeSpan timeout) => _current.WaitUntilIdleAsync(timeout);

    public void Dispose()
    {
        _current.Failed -= OnCurrentFailed;
        _current.Dispose();
    }

    private void OnCurrentFailed(object? sender, Exception error)
    {
        var failedVoice = CurrentVoiceName ?? _current.Id;
        _current.Failed -= OnCurrentFailed;
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
            _current.Failed += OnCurrentFailed;
            return true;
        }
        _current = new NullSpeechEngine();
        CurrentVoiceName = null;
        return false;
    }
}
