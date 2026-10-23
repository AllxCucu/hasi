using System.IO;
using Hasi.Core.Diagnostics;
using Velopack;

namespace Hasi.App;

/// <summary>
/// Entry point. Velopack runs first: during install, update and uninstall it starts hasi.exe with its own
/// arguments, runs the hook and exits, before any window, voice or single-instance check exists.
/// </summary>
internal static class Program
{
    /// <summary>What Velopack reported for this start, read by <see cref="App"/>.</summary>
    public static InstallEvents Events { get; } = new();

    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            // Never restart silently into a downloaded update: updating is always the user's choice, spoken.
            .SetAutoApplyOnStartup(false)
            .OnFirstRun(_ => Events.FirstRun = true)
            .OnRestarted(version => Events.UpdatedTo = version.ToString())
            .OnBeforeUninstallFastCallback(_ => RemoveUserData())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>
    /// Uninstall (the owner's decision, 2026-10-23): autostart, the logs and data in <c>%LOCALAPPDATA%\HASI</c>
    /// and the settings in <c>%APPDATA%\HASI</c> go. Velopack itself removes the program. No UI, and quick:
    /// Velopack ends the process after 30 seconds.
    /// </summary>
    private static void RemoveUserData()
    {
        new RegistryAutostart(NullDiagnosticLog.Instance).SetEnabled(false);
        foreach (var folder in new[] { AppPaths.LocalData, Path.GetDirectoryName(AppPaths.SettingsFile)! })
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file still open by another program: what is left is harmless; uninstalling must go on.
            }
        }
    }
}

internal sealed class InstallEvents
{
    /// <summary>The first start after installing.</summary>
    public bool FirstRun { get; set; }

    /// <summary>Velopack just applied an update to this version.</summary>
    public string? UpdatedTo { get; set; }
}
