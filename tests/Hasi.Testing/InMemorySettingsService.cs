using Hasi.Core.Settings;

namespace Hasi.Testing;

/// <summary>Settings kept in memory, with a fixed voice list, for tests.</summary>
public sealed class InMemorySettingsService : ISettingsService
{
    private readonly List<UserSettings> _history = [];

    public UserSettings Current { get; private set; } = new();

    public IReadOnlyList<VoiceOption> Voices { get; init; } =
    [
        new("rhvoice-dll:Paul", "Paul, RHVoice direct"),
        new("sapi5:Paul", "Paul, prin SAPI"),
        new("sapi5:Microsoft Andrei", "Microsoft Andrei, prin SAPI"),
        new("sapi5:Microsoft David", "Microsoft David, prin SAPI", Romanian: false),
    ];

    public string CurrentVoiceLabel { get; private set; } = "Paul, RHVoice direct";

    /// <summary>Voices that refuse to start, to test the failure message.</summary>
    public HashSet<string> BrokenVoices { get; } = [];

    /// <summary>Every saved state, oldest first.</summary>
    public IReadOnlyList<UserSettings> Saved => _history;

    public event EventHandler<UserSettings>? Changed;

    public void Update(Func<UserSettings, UserSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Current = change(Current).Normalized();
        _history.Add(Current);
        Changed?.Invoke(this, Current);
    }

    public bool SelectVoice(VoiceOption voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        if (BrokenVoices.Contains(voice.Key)) return false;
        CurrentVoiceLabel = voice.Label;
        Update(s => s with { Voice = voice.Key });
        return true;
    }
}
