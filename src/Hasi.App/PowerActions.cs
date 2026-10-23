using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hasi.Core.Diagnostics;
using Hasi.Core.Power;

namespace Hasi.App;

/// <summary>Restarts or shuts down Windows with the system's own <c>shutdown.exe</c>.</summary>
/// <remarks>
/// Never <c>/f</c> unless the user confirmed a forced restart a second time, so programs may still ask to save.
/// Never <c>/hybrid</c>: shutting down is a full shutdown even with Fast Startup on, as a restart always is.
/// <c>/t 0</c> matters: any longer delay implies <c>/f</c>.
/// </remarks>
internal static class PowerActions
{
    private const int SmShuttingDown = 0x2000;

    /// <param name="simulate">Only log what would happen ("Simulare oprire", <c>--simulate-power</c>).</param>
    /// <returns>False when Windows refused at once (for example a policy forbids it).</returns>
    public static bool Run(PowerAction action, bool force, bool simulate, IDiagnosticLog log)
    {
        var arguments = (action == PowerAction.Restart ? "/r" : "/s") + (force ? " /f" : "") + " /t 0";
        if (simulate)
        {
            log.Write("POWER", $"simulated: shutdown.exe {arguments}");
            return true;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo("shutdown.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (process is null) return false;
            process.WaitForExit(5000);
            log.Write("POWER", $"shutdown.exe {arguments} exit code {(process.HasExited ? process.ExitCode : -1)}");
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ErrorLog.Write(ex);
            log.Write("ERROR", $"shutdown.exe failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>True while Windows is ending the session (expected also while its blocking screen is shown).</summary>
    public static bool SessionIsEnding => GetSystemMetrics(SmShuttingDown) != 0;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSystemMetrics(int index);
}
