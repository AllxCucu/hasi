using Hasi.Core.Diagnostics;
using Hasi.Core.Output;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class AnnouncerTests
{
    private sealed class UpperNormalizer : ITextNormalizer
    {
        public string Normalize(string text) => text.ToUpperInvariant();
    }

    private sealed class ThrowingChannel : IOutputChannel
    {
        public void Present(Announcement announcement) => throw new InvalidOperationException("broken");

        public void Cancel() => throw new InvalidOperationException("broken");
    }

    private static (Announcer, RecordingChannel, StringWriter) Create(params IOutputChannel[] extra)
    {
        var channel = new RecordingChannel();
        var writer = new StringWriter();
        var log = new TextDiagnosticLog(writer, TimeProvider.System);
        return (new Announcer(new OutputHub([.. extra, channel], log), new UpperNormalizer(), log), channel, writer);
    }

    [Fact]
    public void Channels_get_original_and_normalized_text()
    {
        var (announcer, channel, _) = Create();

        announcer.Say("Bună");

        var a = Assert.Single(channel.Announcements);
        Assert.Equal("Bună", a.Utterance.Text);
        Assert.Equal("BUNĂ", a.SpokenText);
    }

    [Fact]
    public void Interrupt_cancels_before_presenting_but_queue_does_not()
    {
        var (announcer, channel, _) = Create();

        announcer.Say(new Utterance("a", UtterancePriority.Queue));
        Assert.Equal(0, channel.CancelCount);

        announcer.Say(new Utterance("b"));
        Assert.Equal(1, channel.CancelCount);
    }

    [Fact]
    public void Log_contains_text_and_normalized_text()
    {
        var (announcer, _, writer) = Create();

        announcer.Say("Bună");

        var log = writer.ToString();
        Assert.Contains("SAY    [Interrupt] text=\"Bună\"", log);
        Assert.Contains("       normalized=\"BUNĂ\"", log);
    }

    [Fact]
    public void Sensitive_text_is_masked_in_the_log_but_still_spoken()
    {
        var (announcer, channel, writer) = Create();

        announcer.Say(new Utterance("parola123", Sensitive: true));

        Assert.DoesNotContain("parola123", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PAROLA123", channel.Announcements[0].SpokenText);
    }

    [Fact]
    public void A_broken_channel_does_not_silence_the_others()
    {
        var (announcer, channel, writer) = Create(new ThrowingChannel());

        announcer.Say("Bună");

        Assert.Single(channel.Announcements);
        Assert.Contains("ERROR", writer.ToString());
    }

    [Fact]
    public void Repeat_without_history_does_nothing()
    {
        var (announcer, channel, _) = Create();

        Assert.False(announcer.Repeat());
        Assert.Empty(channel.Announcements);
    }
}
