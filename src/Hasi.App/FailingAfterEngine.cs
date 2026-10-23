using Hasi.Core.Speech;

namespace Hasi.App;

/// <summary>
/// Test aid for <c>--test-voice-failure</c>: behaves like the wrapped voice, then reports a failure after a
/// few messages, so the tester can hear the fallback chain switch voices. Never used otherwise.
/// </summary>
internal sealed class FailingAfterEngine(ISpeechEngine inner, int messagesBeforeFailure) : ISpeechEngine, ISpeechSettingsTarget
{
    private int _spoken;

    public string Id => inner.Id;

    public string? CurrentVoiceName => inner.CurrentVoiceName;

    public bool IsSpeaking => inner.IsSpeaking;

    public event EventHandler<Exception>? Failed;

    public void Speak(string text)
    {
        if (++_spoken > messagesBeforeFailure)
        {
            Failed?.Invoke(this, new InvalidOperationException("Simulated voice failure (--test-voice-failure)."));
            return;
        }
        inner.Speak(text);
    }

    public void Stop() => inner.Stop();

    public void Apply(SpeechSettings settings) => (inner as ISpeechSettingsTarget)?.Apply(settings);

    public Task WaitUntilIdleAsync(TimeSpan timeout) => inner.WaitUntilIdleAsync(timeout);

    public void Dispose() => inner.Dispose();
}
