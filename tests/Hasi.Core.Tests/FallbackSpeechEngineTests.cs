using Hasi.Core.Speech;

namespace Hasi.Core.Tests;

public class FallbackSpeechEngineTests
{
    private sealed class FakeEngine(string id) : ISpeechEngine
    {
        public List<string> Spoken { get; } = [];

        public bool Disposed { get; private set; }

        public string Id { get; } = id;

        public string? CurrentVoiceName => Id;

        public bool IsSpeaking => false;

        public event EventHandler<Exception>? Failed;

        public void Speak(string text) => Spoken.Add(text);

        public void Stop() { }

        public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

        public void Fail() => Failed?.Invoke(this, new InvalidOperationException("audio device lost"));

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Starts_with_the_first_voice_that_works()
    {
        var andrei = new FakeEngine("andrei");
        var failures = new List<string>();

        using var chain = FallbackSpeechEngine.Start(
            [
                new SpeechCandidate("Paul", () => throw new InvalidOperationException("not registered")),
                new SpeechCandidate("Nobody", () => null),
                new SpeechCandidate("Microsoft Andrei", () => andrei),
            ],
            (voice, _) => failures.Add(voice));

        chain.Speak("Bună");

        Assert.Equal("Microsoft Andrei", chain.CurrentVoiceName);
        Assert.Equal(["Bună"], andrei.Spoken);
        Assert.Equal(["Paul"], failures);
    }

    [Fact]
    public void A_voice_that_fails_while_running_is_replaced_by_the_next_and_announced()
    {
        var paul = new FakeEngine("paul");
        var andrei = new FakeEngine("andrei");
        using var chain = FallbackSpeechEngine.Start(
            [new SpeechCandidate("Paul", () => paul), new SpeechCandidate("Microsoft Andrei", () => andrei)]);
        VoiceSwitchedEventArgs? switched = null;
        chain.VoiceSwitched += (_, e) => switched = e;

        paul.Fail();
        chain.Speak("Încă aici");

        Assert.Equal("Paul", switched!.FailedVoice);
        Assert.Equal("Microsoft Andrei", switched.NewVoice);
        Assert.True(paul.Disposed);
        Assert.Equal(["Încă aici"], andrei.Spoken);
    }

    [Fact]
    public void When_every_voice_has_failed_the_chain_reports_failure_and_stays_silent_safely()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        Exception? failure = null;
        chain.Failed += (_, e) => failure = e;

        paul.Fail();
        chain.Speak("Nimeni nu aude");

        Assert.NotNull(failure);
        Assert.Null(chain.CurrentVoiceName);
        Assert.Equal("none", chain.Id);
    }

    [Fact]
    public void No_candidates_means_no_voice()
    {
        using var chain = FallbackSpeechEngine.Start([]);

        Assert.Null(chain.CurrentVoiceName);
    }
}
