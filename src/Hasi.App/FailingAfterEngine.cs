using Hasi.Core.Output;
using Hasi.Core.Speech;

namespace Hasi.App;

/// <summary>
/// Test aid for <c>--test-voice-failure</c>: behaves like the wrapped voice, then reports a failure after a
/// few messages, so the tester can hear the fallback chain switch voices. Never used otherwise.
/// </summary>
internal sealed class FailingAfterEngine : ISpeechEngine, ISpeechSettingsTarget
{
    private readonly ISpeechEngine _inner;
    private readonly int _messagesBeforeFailure;
    private int _spoken;

    public FailingAfterEngine(ISpeechEngine inner, int messagesBeforeFailure)
    {
        _inner = inner;
        _messagesBeforeFailure = messagesBeforeFailure;
        _inner.Finished += (_, finished) => Finished?.Invoke(this, finished);
    }

    public string Id => _inner.Id;

    public string? CurrentVoiceName => _inner.CurrentVoiceName;

    public bool IsSpeaking => _inner.IsSpeaking;

    public event EventHandler<Exception>? Failed;

    public event EventHandler<SpeechFinished>? Finished;

    public void Speak(string text, long id)
    {
        if (++_spoken > _messagesBeforeFailure)
        {
            Finished?.Invoke(this, new SpeechFinished(id, SpeechOutcome.Failed));
            Failed?.Invoke(this, new InvalidOperationException("Simulated voice failure (--test-voice-failure)."));
            return;
        }
        _inner.Speak(text, id);
    }

    public void Stop() => _inner.Stop();

    public void Apply(SpeechSettings settings) => (_inner as ISpeechSettingsTarget)?.Apply(settings);

    public Task WaitUntilIdleAsync(TimeSpan timeout) => _inner.WaitUntilIdleAsync(timeout);

    public void Dispose() => _inner.Dispose();
}
