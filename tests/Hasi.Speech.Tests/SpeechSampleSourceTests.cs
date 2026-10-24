using Hasi.Speech.Audio;

namespace Hasi.Speech.Tests;

public class SpeechSampleSourceTests
{
    private const int Rate = 1000; // 1 sample = 1 ms keeps the arithmetic readable
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(8);

    private static short[] Tone(int count, short value = 16384) => Enumerable.Repeat(value, count).ToArray();

    private static float[] Read(SpeechSampleSource source, int count)
    {
        var buffer = new float[count];
        Assert.Equal(count, source.Read(buffer));
        return buffer;
    }

    [Fact]
    public void Plays_silence_when_there_is_nothing_to_say_so_the_device_stays_open()
    {
        var source = new SpeechSampleSource(Rate, Fade);

        Assert.All(Read(source, 100), s => Assert.Equal(0f, s));
        Assert.False(source.IsPlaying);
    }

    [Fact]
    public void Plays_written_samples_with_the_volume_applied()
    {
        var source = new SpeechSampleSource(Rate, Fade) { Gain = 0.5f };
        source.Write(1, Tone(10));

        var played = Read(source, 20);

        Assert.All(played[..10], s => Assert.Equal(0.25f, s, precision: 4));
        Assert.All(played[10..], s => Assert.Equal(0f, s));
    }

    [Fact]
    public void Reports_first_sound_and_end_of_each_utterance()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        var events = new List<string>();
        source.FirstSoundPlayed += id => events.Add($"start {id}");
        source.UtteranceFinished += id => events.Add($"end {id}");
        source.Write(1, Tone(5));
        source.Write(1, Tone(5));
        source.EndUtterance(1);
        source.Write(2, Tone(5));
        source.EndUtterance(2);

        Read(source, 40);

        Assert.Equal(["start 1", "end 1", "start 2", "end 2"], events);
    }

    [Fact]
    public void Stop_fades_out_in_a_few_milliseconds_without_a_click_then_drops_everything()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        var completed = 0;
        source.FadeOutCompleted += () => completed++;
        source.Write(1, Tone(100));
        source.Write(2, Tone(100));
        Read(source, 10);

        source.FadeOutAndClear();
        var after = Read(source, 50);

        var fade = after[..8];
        for (var i = 1; i < fade.Length; i++) Assert.True(fade[i] < fade[i - 1], "the fade must go down smoothly");
        Assert.True(fade[0] < 0.5f && fade[0] > 0.3f, "the fade starts near the current level");
        Assert.All(after[8..], s => Assert.Equal(0f, s));
        Assert.Equal(1, completed);
        Assert.False(source.IsPlaying);
        Assert.Equal(TimeSpan.Zero, source.Buffered);
    }

    [Fact]
    public void Stop_when_nothing_is_playing_completes_at_once()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        var completed = 0;
        source.FadeOutCompleted += () => completed++;

        source.FadeOutAndClear();

        Assert.Equal(1, completed);
    }

    [Fact]
    public void Speech_after_a_stop_plays_normally()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        source.Write(1, Tone(100));
        Read(source, 10);
        source.FadeOutAndClear();
        Read(source, 20);

        source.Write(2, Tone(10));

        Assert.All(Read(source, 10), s => Assert.Equal(0.5f, s, precision: 4));
    }

    [Fact]
    public void Silence_can_be_inserted_before_speech_for_example_after_a_device_change()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        source.InsertSilence(TimeSpan.FromMilliseconds(30));
        source.Write(1, Tone(10));

        var played = Read(source, 40);

        Assert.All(played[..30], s => Assert.Equal(0f, s));
        Assert.All(played[30..], s => Assert.Equal(0.5f, s, precision: 4));
    }

    [Fact]
    public void With_an_output_delay_the_end_is_reported_only_when_the_device_has_played_it()
    {
        // The device holds 20 ms; each read hands over 10 ms more. The utterance ends at 5 ms.
        var source = new SpeechSampleSource(Rate, Fade) { OutputDelay = () => TimeSpan.FromMilliseconds(20) };
        var ended = new List<int>();
        source.UtteranceFinished += ended.Add;
        source.Write(1, Tone(5));
        source.EndUtterance(1);

        Read(source, 10); // handed over 0-10 ms: nothing heard yet
        Read(source, 10);
        Read(source, 10); // heard up to 0 ms
        Assert.Empty(ended);
        Assert.True(source.IsPlaying);

        Read(source, 10); // heard up to 10 ms
        Assert.Equal([1], ended);
        Assert.False(source.IsPlaying);

        Read(source, 10);
        Assert.Equal([1], ended); // exactly once
    }

    [Fact]
    public void A_stop_drops_an_end_not_heard_yet()
    {
        var source = new SpeechSampleSource(Rate, Fade) { OutputDelay = () => TimeSpan.FromMilliseconds(20) };
        var ended = new List<int>();
        source.UtteranceFinished += ended.Add;
        source.Write(1, Tone(5));
        source.EndUtterance(1);
        Read(source, 10);

        source.FadeOutAndClear();
        for (var i = 0; i < 5; i++) Read(source, 10);

        Assert.Empty(ended);
        Assert.False(source.IsPlaying);
    }

    [Fact]
    public void Buffered_tells_how_much_speech_is_waiting()
    {
        var source = new SpeechSampleSource(Rate, Fade);
        source.Write(1, Tone(500));
        Read(source, 200);

        Assert.Equal(300, source.Buffered.TotalMilliseconds, precision: 0);
    }
}
