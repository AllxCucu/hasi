using Hasi.Core.Diagnostics;
using Hasi.Core.Output;
using Hasi.Core.Speech;

namespace Hasi.Core.Tests;

public class SpeechTests
{
    private sealed class FakeEngine : ISpeechEngine
    {
        public List<string> Spoken { get; } = [];

        public int Stops { get; private set; }

        public string Id => "fake";

        public string? CurrentVoiceName => "Fake";

        public bool IsSpeaking { get; set; }

        public void Speak(string text, long id) => Spoken.Add(text);

        public void Stop() => Stops++;

        public Task WaitUntilIdleAsync(TimeSpan timeout) => Task.CompletedTask;

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

        public void Dispose() { }
    }

    private sealed class FakeEarcons : IEarconPlayer
    {
        public List<Earcon> Played { get; } = [];

        public void Play(Earcon earcon) => Played.Add(earcon);
    }

    [Fact]
    public void Channel_speaks_the_normalized_text_and_logs_it()
    {
        var engine = new FakeEngine();
        var writer = new StringWriter();
        var channel = new SpeechOutputChannel(engine, new FakeEarcons(), new TextDiagnosticLog(writer, TimeProvider.System));

        channel.Present(new Announcement(new Utterance("1 leu"), "un leu"));

        Assert.Equal(["un leu"], engine.Spoken);
        Assert.Contains("SPEAK  engine=fake text=\"un leu\"", writer.ToString());
    }

    [Fact]
    public void Channel_plays_earcon_before_speaking()
    {
        var engine = new FakeEngine();
        var earcons = new FakeEarcons();
        var channel = new SpeechOutputChannel(engine, earcons, NullDiagnosticLog.Instance);

        channel.Present(new Announcement(new Utterance("Sfârșitul listei.", Earcon: Earcon.EdgeOfList), "Sfârșitul listei."));

        Assert.Equal([Earcon.EdgeOfList], earcons.Played);
    }

    [Fact]
    public void Sensitive_text_is_masked_in_the_speak_log()
    {
        var writer = new StringWriter();
        var channel = new SpeechOutputChannel(new FakeEngine(), new FakeEarcons(), new TextDiagnosticLog(writer, TimeProvider.System));

        channel.Present(new Announcement(new Utterance("secret", Sensitive: true), "secret"));

        Assert.DoesNotContain("secret", writer.ToString());
    }

    [Fact]
    public void Cancel_stops_the_engine_and_logs_only_when_it_was_speaking()
    {
        var engine = new FakeEngine();
        var writer = new StringWriter();
        var channel = new SpeechOutputChannel(engine, new FakeEarcons(), new TextDiagnosticLog(writer, TimeProvider.System));

        channel.Cancel();
        Assert.DoesNotContain("STOP", writer.ToString());

        engine.IsSpeaking = true;
        channel.Cancel();
        Assert.Contains("STOP", writer.ToString());
        Assert.Equal(2, engine.Stops);
    }

    private static readonly VoiceCandidate David = new("Microsoft David Desktop", "en-US");
    private static readonly VoiceCandidate Andrei = new("Microsoft Andrei", "ro-RO");
    private static readonly VoiceCandidate Paul = new("Paul", "ro-RO");

    [Fact]
    public void Prefers_paul()
    {
        Assert.Equal(Paul, VoiceChooser.Choose([David, Andrei, Paul], "Paul"));
    }

    [Fact]
    public void Falls_back_to_a_romanian_voice()
    {
        Assert.Equal(Andrei, VoiceChooser.Choose([David, Andrei], "Paul"));
    }

    [Fact]
    public void Falls_back_to_any_voice()
    {
        Assert.Equal(David, VoiceChooser.Choose([David], "Paul"));
    }

    [Fact]
    public void Ranking_lists_every_voice_best_first_for_fallback()
    {
        Assert.Equal([Paul, Andrei, David], VoiceChooser.Rank([David, Andrei, Paul], "Paul"));
    }

    [Fact]
    public void No_voices_means_no_choice()
    {
        Assert.Null(VoiceChooser.Choose([], "Paul"));
    }
}
