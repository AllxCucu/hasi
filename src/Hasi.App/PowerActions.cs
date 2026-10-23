using System.ComponentModel;
using System.Diagnostics;
using Hasi.Core.Diagnostics;

namespace Hasi.App;

internal enum PowerAction
{
    None,
    Restart,
    Shutdown,
}

/// <summary>Restarts or shuts down Windows with the system's own <c>shutdown.exe</c>.</summary>
internal static class PowerActions
{
    /// <param name="simulate"><c>--simulate-power</c>: only log what would happen (for testing).</param>
    /// <returns>False when Windows refused (for example a policy forbids it); HASI then stays open.</returns>
    public static bool Run(PowerAction action, bool simulate, IDiagnosticLog log)
    {
        var arguments = action == PowerAction.Restart ? "/r /t 0" : "/s /t 0";
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
}
