using Hasi.Speech.Audio;

namespace Hasi.Speech.Tests;

public class IdlePolicyTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void Closes_an_open_silent_stream_after_the_timeout()
    {
        Assert.True(IdlePolicy.ShouldClose(isOpen: true, isPlaying: false, TimeSpan.FromSeconds(61), Minute));
    }

    [Fact]
    public void Keeps_the_stream_before_the_timeout()
    {
        Assert.False(IdlePolicy.ShouldClose(isOpen: true, isPlaying: false, TimeSpan.FromSeconds(59), Minute));
    }

    [Fact]
    public void Never_closes_while_speaking()
    {
        Assert.False(IdlePolicy.ShouldClose(isOpen: true, isPlaying: true, TimeSpan.FromHours(1), Minute));
    }

    [Fact]
    public void A_zero_timeout_keeps_the_stream_open_forever()
    {
        Assert.False(IdlePolicy.ShouldClose(isOpen: true, isPlaying: false, TimeSpan.FromHours(1), TimeSpan.Zero));
    }

    [Fact]
    public void Nothing_to_close_when_already_closed()
    {
        Assert.False(IdlePolicy.ShouldClose(isOpen: false, isPlaying: false, TimeSpan.FromHours(1), Minute));
    }
}
