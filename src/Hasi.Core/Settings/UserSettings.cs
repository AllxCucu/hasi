using System.Text.Json;
using System.Text.Json.Serialization;
using Hasi.Core.Speech;

namespace Hasi.Core.Settings;

/// <summary>What is spoken while typing in a text field.</summary>
public enum KeyEcho
{
    Character,
    Word,
    None,
}

/// <summary>High-contrast colour pairs for the large text (text on background).</summary>
public enum ColorScheme
{
    YellowOnBlack,
    WhiteOnBlack,
    BlackOnWhite,
    BlackOnYellow,
}

public enum TextSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
    Huge,
}

/// <summary>
/// Everything the user can change in Settings, saved in <c>%APPDATA%\HASI\settings.json</c>.
/// Rate and pitch are -10..10 (0 = the voice's default), volume 0..100.
/// </summary>
public sealed record UserSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>The chosen voice as "engine:name" (for example "rhvoice-dll:Paul"); null = HASI's default.</summary>
    public string? Voice { get; init; }

    public int Rate { get; init; }

    public int Pitch { get; init; }

    public int Volume { get; init; } = 100;

    public KeyEcho KeyEcho { get; init; } = KeyEcho.Character;

    public ColorScheme Colors { get; init; } = ColorScheme.YellowOnBlack;

    public TextSize TextSize { get; init; } = TextSize.Large;

    public bool WrapLists { get; init; }

    /// <summary>Close the audio stream after this many seconds without speech; 0 = keep it open.</summary>
    public int AudioIdleSeconds { get; init; } = 60;

    /// <summary>
    /// "Simulare oprire": restart and shut down only pretend (HASI closes, the computer stays on).
    /// Off by default since phase 0.11; files that saved it on keep it on (ADR 0006).
    /// </summary>
    public bool SimulatePower { get; init; }

    /// <summary>Set when the user turns <see cref="SimulatePower"/> off: the next start warns once that they are real.</summary>
    public bool RealPowerNoticePending { get; init; }

    /// <summary>Look for a new version at every start (Settings → Actualizări).</summary>
    public bool AutoCheckUpdates { get; init; } = true;

    /// <summary>The newest version HASI already offered at startup; it is not offered again, only a newer one.</summary>
    public string? UpdateOfferedVersion { get; init; }

    /// <summary>Where updates come from (<see cref="Updates.UpdateSource"/>); null = HASI's GitHub Releases.</summary>
    public string? UpdateSource { get; init; }

    [JsonIgnore]
    public SpeechSettings Speech => new(Rate, Pitch, Volume);

    /// <summary>Puts every value back in its allowed range.</summary>
    public UserSettings Normalized() => this with
    {
        Rate = Math.Clamp(Rate, -10, 10),
        Pitch = Math.Clamp(Pitch, -10, 10),
        Volume = Math.Clamp(Volume, 0, 100),
        AudioIdleSeconds = Math.Max(0, AudioIdleSeconds),
        KeyEcho = Enum.IsDefined(KeyEcho) ? KeyEcho : KeyEcho.Character,
        Colors = Enum.IsDefined(Colors) ? Colors : ColorScheme.YellowOnBlack,
        TextSize = Enum.IsDefined(TextSize) ? TextSize : TextSize.Large,
    };
}

/// <summary>Reads and writes <see cref="UserSettings"/> as JSON. Writing is atomic: a crash never leaves half a file.</summary>
public static class SettingsFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Loads the settings. A missing file gives the defaults. A damaged file is kept as
    /// <c>settings.json.bad</c> and the defaults are used; <paramref name="problem"/> then says why.
    /// </summary>
    public static UserSettings Load(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path)) return new UserSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), Json) ?? new UserSettings();
            return Migrate(settings).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            problem = ex.Message;
            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                problem += $" (backup failed: {copy.Message})";
            }
            return new UserSettings();
        }
    }

    public static void Save(string path, UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings with { SchemaVersion = UserSettings.CurrentSchemaVersion }, Json));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Upgrades older files. Version 1 is the first; future versions add their steps here.</summary>
    private static UserSettings Migrate(UserSettings settings) =>
        settings with { SchemaVersion = UserSettings.CurrentSchemaVersion };
}

/// <summary>
/// Values given on the command line for testing. They win for the session; a value given there is written
/// to the settings file only if the user changes it in Settings.
/// </summary>
public sealed record SettingsOverrides(int? Rate = null, int? Pitch = null, int? Volume = null, int? AudioIdleSeconds = null)
{
    public UserSettings ApplyTo(UserSettings saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        return (saved with
        {
            Rate = Rate ?? saved.Rate,
            Pitch = Pitch ?? saved.Pitch,
            Volume = Volume ?? saved.Volume,
            AudioIdleSeconds = AudioIdleSeconds ?? saved.AudioIdleSeconds,
        }).Normalized();
    }

    /// <summary>What to save after the user changed <paramref name="before"/> into <paramref name="after"/>.</summary>
    public static UserSettings ToSave(UserSettings saved, UserSettings before, UserSettings after)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return (after with
        {
            Rate = after.Rate != before.Rate ? after.Rate : saved.Rate,
            Pitch = after.Pitch != before.Pitch ? after.Pitch : saved.Pitch,
            Volume = after.Volume != before.Volume ? after.Volume : saved.Volume,
            AudioIdleSeconds = after.AudioIdleSeconds != before.AudioIdleSeconds ? after.AudioIdleSeconds : saved.AudioIdleSeconds,
        }).Normalized();
    }
}

/// <summary>A voice the user can choose in Settings.</summary>
/// <param name="Key">"engine:name", stored in <see cref="UserSettings.Voice"/>.</param>
/// <param name="Label">What is said, for example "Paul, RHVoice direct".</param>
/// <param name="Romanian">Romanian voices are listed by default; the others only after "Arată toate vocile".</param>
public sealed record VoiceOption(string Key, string Label, bool Romanian = true);

/// <summary>Settings as the modules see them: read, change (saved and applied at once), and the voice list.</summary>
public interface ISettingsService
{
    UserSettings Current { get; }

    IReadOnlyList<VoiceOption> Voices { get; }

    /// <summary>The voice speaking now.</summary>
    string CurrentVoiceLabel { get; }

    /// <summary>Applies a change, saves it and notifies the application (speech, colours, text size …).</summary>
    void Update(Func<UserSettings, UserSettings> change);

    /// <summary>Switches to another voice; false if it could not start (the previous voice stays).</summary>
    bool SelectVoice(VoiceOption voice);
}

public static class SettingsServiceExtensions
{
    /// <summary>Turns "Simulare oprire" on or off; turning it off arms the one-time warning for the next start.</summary>
    public static void SetSimulatePower(this ISettingsService settings, bool simulate)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Update(u => u with
        {
            SimulatePower = simulate,
            RealPowerNoticePending = !simulate && (u.SimulatePower || u.RealPowerNoticePending),
        });
    }

    /// <summary>True once, at the first start after "Simulare oprire" was turned off.</summary>
    public static bool TakeRealPowerNotice(this ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Current.RealPowerNoticePending) return false;
        settings.Update(u => u with { RealPowerNoticePending = false });
        return !settings.Current.SimulatePower;
    }
}
