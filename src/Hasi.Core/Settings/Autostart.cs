namespace Hasi.Core.Settings;

/// <summary>
/// "Pornire automată": whether HASI starts by itself when the user signs in to Windows.
/// Kept by Windows (not in settings.json), so it is read from there every time.
/// </summary>
public interface IAutostartService
{
    bool IsEnabled { get; }

    /// <summary>False when Windows refused the change.</summary>
    bool SetEnabled(bool enabled);
}
