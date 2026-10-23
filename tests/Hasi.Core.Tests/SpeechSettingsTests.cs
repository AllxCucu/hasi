using Hasi.Core.Speech;

namespace Hasi.Core.Tests;

public class SpeechSettingsTests
{
    [Fact]
    public void Defaults_are_the_voices_own_rate_and_pitch_at_full_volume()
    {
        var s = new SpeechSettings();

        Assert.Equal((0, 0, 100), (s.Rate, s.Pitch, s.Volume));
        Assert.Equal(1f, s.Gain);
        Assert.Equal(1.0, s.OneCoreRate, precision: 6);
        Assert.Equal(0.0, s.RhVoiceRate);
    }

    [Fact]
    public void Values_are_clamped_to_hasis_scale()
    {
        var s = new SpeechSettings(Rate: 50, Pitch: -50, Volume: 150);

        Assert.Equal((10, -10, 100), (s.Rate, s.Pitch, s.Volume));
    }

    [Theory]
    [InlineData(-10, -1.0, 0.5)]
    [InlineData(-5, -0.5, 0.577)]
    [InlineData(5, 0.5, 1.732)]
    [InlineData(10, 1.0, 3.0)]
    public void Rate_maps_to_rhvoice_and_onecore(int rate, double rhvoice, double oneCore)
    {
        var s = new SpeechSettings(Rate: rate);

        Assert.Equal(rhvoice, s.RhVoiceRate, precision: 6);
        Assert.Equal(oneCore, s.OneCoreRate, precision: 3);
    }

    [Fact]
    public void Volume_is_a_linear_gain()
    {
        Assert.Equal(0.25f, new SpeechSettings(Volume: 25).Gain);
    }
}
