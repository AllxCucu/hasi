using Hasi.Core.Output;
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

        public List<long> Ids { get; } = [];

        public int Stops { get; private set; }

        public event EventHandler<Exception>? Failed;

        public event EventHandler<SpeechFinished>? Finished;

        public void Speak(string text, long id)
        {
            Spoken.Add(text);
            Ids.Add(id);
        }

        public void Stop() => Stops++;

        public void Report(long id, SpeechOutcome outcome) => Finished?.Invoke(this, new SpeechFinished(id, outcome));

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

    private sealed class TunableEngine(string id) : ISpeechEngine, ISpeechSettingsTarget
    {
        public SpeechSettings? Applied { get; private set; }

        public string Id { get; } = id;

        public string? CurrentVoiceName => Id;

        public bool IsSpeaking => false;

        public event EventHandler<Exception>? Failed
        {
            add { }
            remove { }
        }

        public event EventHandler<SpeechFinished>? Finished
        {
            add { }
            remove { }
        }

        public void Speak(string text, long id) { }

        public void Stop() { }

        public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

        public void Apply(SpeechSettings settings) => Applied = settings;

        public void Dispose() { }
    }

    [Fact]
    public void Settings_reach_the_current_voice_and_every_voice_started_later()
    {
        var first = new TunableEngine("a");
        var second = new TunableEngine("b");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("A", () => first, "x:a")]);
        var slow = new SpeechSettings(Rate: -4);

        chain.Apply(slow);
        chain.Restart([new SpeechCandidate("B", () => second, "x:b")]);

        Assert.Equal(slow, first.Applied);
        Assert.Equal(slow, second.Applied);
        Assert.Equal("x:b", chain.CurrentKey);
    }

    [Fact]
    public void Restart_with_voices_that_do_not_start_reports_it()
    {
        var first = new TunableEngine("a");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("A", () => first, "x:a")]);

        Assert.False(chain.Restart([new SpeechCandidate("B", () => null, "x:b")]));
        Assert.Null(chain.CurrentKey);
    }

    [Fact]
    public void No_candidates_means_no_voice()
    {
        using var chain = FallbackSpeechEngine.Start([]);

        Assert.Null(chain.CurrentVoiceName);
    }

    private static List<SpeechFinished> Reports(FallbackSpeechEngine chain)
    {
        var reports = new List<SpeechFinished>();
        chain.Finished += (_, finished) => reports.Add(finished);
        return reports;
    }

    [Fact]
    public void Reports_of_the_voice_that_spoke_are_passed_on_once()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        var reports = Reports(chain);

        chain.Speak("unu", 1);
        paul.Report(1, SpeechOutcome.Completed);
        paul.Report(1, SpeechOutcome.Completed); // an engine bug: never reported twice

        Assert.Equal([1L], paul.Ids);
        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Completed)], reports);
    }

    [Fact]
    public void When_the_voice_fails_what_it_still_owed_is_reported_failed_and_the_next_voice_reports_its_own()
    {
        var paul = new FakeEngine("paul");
        var andrei = new FakeEngine("andrei");
        using var chain = FallbackSpeechEngine.Start(
            [new SpeechCandidate("Paul", () => paul), new SpeechCandidate("Microsoft Andrei", () => andrei)]);
        var reports = Reports(chain);
        chain.Speak("unu", 1);
        chain.Speak("doi", 2);
        chain.Speak("trei", 3);
        paul.Report(1, SpeechOutcome.Completed);

        paul.Fail();
        paul.Report(2, SpeechOutcome.Cancelled); // late, from the voice already thrown away
        chain.Speak("patru", 4);
        andrei.Report(4, SpeechOutcome.Completed);

        Assert.Equal(
            [
                new SpeechFinished(1, SpeechOutcome.Completed),
                new SpeechFinished(2, SpeechOutcome.Failed),
                new SpeechFinished(3, SpeechOutcome.Failed),
                new SpeechFinished(4, SpeechOutcome.Completed),
            ],
            reports);
        Assert.Equal(["patru"], andrei.Spoken); // not spoken again by the new voice
    }

    [Fact]
    public void A_voice_that_reports_the_failing_message_itself_before_failing_is_not_reported_twice()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start(
            [new SpeechCandidate("Paul", () => paul), new SpeechCandidate("Andrei", () => new FakeEngine("andrei"))]);
        var reports = Reports(chain);
        chain.Speak("unu", 1);

        paul.Report(1, SpeechOutcome.Failed);
        paul.Fail();

        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Failed)], reports);
    }

    [Fact]
    public void Choosing_another_voice_reports_what_the_old_one_still_owed_as_cancelled()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        var reports = Reports(chain);
        chain.Speak("unu", 1);

        chain.Restart([new SpeechCandidate("Andrei", () => new FakeEngine("andrei"))]);
        paul.Report(1, SpeechOutcome.Cancelled);

        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Cancelled)], reports);
    }

    [Fact]
    public void Without_any_voice_every_message_is_reported_failed()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        var reports = Reports(chain);
        paul.Fail();

        chain.Speak("nimeni", 5);

        Assert.Equal([new SpeechFinished(5, SpeechOutcome.Failed)], reports);
    }

    [Fact]
    public void Stop_is_passed_to_the_voice_which_reports_the_cancelled_messages()
    {
        var paul = new FakeEngine("paul");
        using var chain = FallbackSpeechEngine.Start([new SpeechCandidate("Paul", () => paul)]);
        var reports = Reports(chain);
        chain.Speak("unu", 1);

        chain.Stop();
        paul.Report(1, SpeechOutcome.Cancelled);

        Assert.Equal(1, paul.Stops);
        Assert.Equal([new SpeechFinished(1, SpeechOutcome.Cancelled)], reports);
    }
}
