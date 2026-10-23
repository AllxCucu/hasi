using System.IO;
using Hasi.Core.Diagnostics;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Speech;

namespace Hasi.App;

/// <summary>
/// Settings saved in <c>%APPDATA%\HASI\settings.json</c> and applied at once: speech to the voice chain, list ends
/// and key echo to the shell, colours and text size to the window.
/// Command-line options (for testing) override the saved values for this session only: a setting given on the
/// command line is saved only if the user changes it in Settings.
/// </summary>
internal sealed class AppSettingsService : ISettingsService
{
    private readonly string _path;
    private readonly StartupOptions _options;
    private readonly SpeechSetup _speech;
    private readonly FallbackSpeechEngine _chain;
    private readonly ShellOptions _shellOptions;
    private readonly ShellWindow _window;
    private readonly IDiagnosticLog _log;
    private UserSettings _saved;

    public AppSettingsService(
        string path, UserSettings saved, StartupOptions options, SpeechSetup speech, FallbackSpeechEngine chain,
        ShellOptions shellOptions, ShellWindow window, IDiagnosticLog log)
    {
        _path = path;
        _saved = saved;
        _options = options;
        _speech = speech;
        _chain = chain;
        _shellOptions = shellOptions;
        _window = window;
        _log = log;
        Current = Effective(saved, options);
        ApplyNonSpeech(Current);
    }

    public UserSettings Current { get; private set; }

    public IReadOnlyList<VoiceOption> Voices => _speech.VoiceOptions;

    public string CurrentVoiceLabel => _chain.CurrentVoiceName ?? "";

    /// <summary>Raised when a change could not be written to disk (it still applies until HASI closes).</summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>The saved settings with this session's command-line overrides on top.</summary>
    public static UserSettings Effective(UserSettings saved, StartupOptions options) =>
        new SettingsOverrides(options.Rate, options.Pitch, options.Volume, options.AudioIdleSeconds).ApplyTo(saved);

    public void Update(Func<UserSettings, UserSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var before = Current;
        var after = change(before).Normalized();
        // Only what the user changed now is saved; untouched command-line overrides stay out of the file.
        _saved = SettingsOverrides.ToSave(_saved, before, after);
        Current = after;
        Save();
        _log.Write("SETTINGS", Describe(after));
        if (after.Speech != before.Speech)
        {
            _speech.Settings = after.Speech;
            _chain.Apply(after.Speech);
        }
        ApplyNonSpeech(after);
    }

    public bool SelectVoice(VoiceOption voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var previous = _chain.CurrentKey;
        if (_chain.Restart(_speech.Candidates(voice.Key), _speech.LogStartFailure) && _chain.CurrentKey == voice.Key)
        {
            _speech.SavedVoiceKey = voice.Key;
            Update(s => s with { Voice = voice.Key });
            _log.Write("ENGINE", $"user chose \"{voice.Label}\"");
            return true;
        }
        _log.Write("ENGINE", $"\"{voice.Label}\" did not start; back to the previous voice");
        _chain.Restart(_speech.Candidates(previous), _speech.LogStartFailure);
        return false;
    }

    private void ApplyNonSpeech(UserSettings settings)
    {
        _shellOptions.WrapLists = settings.WrapLists;
        _shellOptions.KeyEcho = settings.KeyEcho;
        _window.ApplyAppearance(settings.Colors, settings.TextSize);
    }

    private void Save()
    {
        try
        {
            SettingsFile.Save(_path, _saved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"settings not saved: {ex.Message}");
            SaveFailed?.Invoke(this, ex);
        }
    }

    private static string Describe(UserSettings s) =>
        $"voice={s.Voice ?? "default"} rate={s.Rate} pitch={s.Pitch} volume={s.Volume} echo={s.KeyEcho} " +
        $"colors={s.Colors} text={s.TextSize} wrap={s.WrapLists} audio-idle={s.AudioIdleSeconds}s";
}
