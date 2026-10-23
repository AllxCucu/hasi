using Hasi.Core.Settings;

namespace Hasi.Core.Tests;

public sealed class SettingsFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hasi-settings-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        var settings = SettingsFile.Load(Path_, out var problem);

        Assert.Null(problem);
        Assert.Equal(new UserSettings(), settings);
        Assert.Equal(KeyEcho.Character, settings.KeyEcho);
        Assert.Equal(TextSize.Large, settings.TextSize);
        Assert.Equal(60, settings.AudioIdleSeconds);
    }

    [Fact]
    public void A_file_without_simulare_oprire_gets_the_default_off()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """{ "SchemaVersion": 1, "Rate": 2 }""");

        var settings = SettingsFile.Load(Path_, out _);

        Assert.False(settings.SimulatePower);
        Assert.False(settings.RealPowerNoticePending);
        Assert.True(settings.AutoCheckUpdates);
        Assert.Null(settings.UpdateOfferedVersion);
        Assert.Null(settings.UpdateSource);
    }

    [Fact]
    public void A_file_that_saved_simulare_oprire_on_keeps_it_on_no_migration()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """{ "SchemaVersion": 1, "SimulatePower": true }""");

        Assert.True(SettingsFile.Load(Path_, out _).SimulatePower);
    }

    [Fact]
    public void Settings_survive_a_save_and_a_load()
    {
        var saved = new UserSettings
        {
            Voice = "rhvoice-dll:Paul", Rate = -3, Pitch = 2, Volume = 70, KeyEcho = KeyEcho.Word,
            Colors = ColorScheme.BlackOnYellow, TextSize = TextSize.Huge, WrapLists = true, AudioIdleSeconds = 30,
        };

        SettingsFile.Save(Path_, saved);

        Assert.Equal(saved, SettingsFile.Load(Path_, out _));
        Assert.Contains("\"BlackOnYellow\"", File.ReadAllText(Path_)); // readable names, not numbers
        Assert.False(File.Exists(Path_ + ".tmp"));
    }

    [Fact]
    public void A_damaged_file_is_kept_aside_and_the_defaults_are_used()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, "{ acesta nu este json");

        var settings = SettingsFile.Load(Path_, out var problem);

        Assert.NotNull(problem);
        Assert.Equal(new UserSettings(), settings);
        Assert.True(File.Exists(Path_ + ".bad"));
    }

    [Fact]
    public void Values_out_of_range_are_brought_back()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """{ "Rate": 99, "Volume": -5, "AudioIdleSeconds": -1 }""");

        var settings = SettingsFile.Load(Path_, out _);

        Assert.Equal((10, 0, 0), (settings.Rate, settings.Volume, settings.AudioIdleSeconds));
    }

    [Fact]
    public void Command_line_values_win_for_the_session_but_are_saved_only_when_changed()
    {
        var saved = new UserSettings { Rate = 2, Volume = 80 };
        var overrides = new SettingsOverrides(Rate: -5);

        var effective = overrides.ApplyTo(saved);
        Assert.Equal(-5, effective.Rate);

        // The user changes only the volume: the -5 from the command line must not be written.
        var afterVolume = effective with { Volume = 60 };
        var toSave = SettingsOverrides.ToSave(saved, effective, afterVolume);
        Assert.Equal((2, 60), (toSave.Rate, toSave.Volume));

        // Now the user changes the rate itself: that is saved.
        var afterRate = afterVolume with { Rate = 4 };
        Assert.Equal(4, SettingsOverrides.ToSave(toSave, afterVolume, afterRate).Rate);
    }
}
