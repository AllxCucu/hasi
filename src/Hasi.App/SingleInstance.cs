using System.Runtime.InteropServices;
using Hasi.Core.Diagnostics;
using Microsoft.Win32;

namespace Hasi.App;

/// <summary>
/// One HASI per Windows session. Starting it again (a second click, autostart plus a manual start) does not open
/// a second window: the running HASI comes forward and says where the user is.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\HASI-single-instance";
    private const string ComeForwardName = @"Local\HASI-come-forward";
    private const int AsfwAny = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _comeForward;
    private readonly ManualResetEvent _stop = new(false);

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _comeForward = new EventWaitHandle(false, EventResetMode.AutoReset, ComeForwardName);
    }

    /// <summary>
    /// Becomes the one running HASI, or returns null when another one runs. After a serious error the previous
    /// HASI is still ending, so <paramref name="waitForPrevious"/> waits a little for it.
    /// </summary>
    public static SingleInstance? TryAcquire(bool waitForPrevious)
    {
        var mutex = new Mutex(false, MutexName);
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(waitForPrevious ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            acquired = true; // The previous HASI ended without releasing it (a crash).
        }
        if (acquired) return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    /// <summary>Asks the running HASI to come forward. Called by the second instance, which then exits.</summary>
    public static void AskRunningToComeForward()
    {
        // The second instance was started by the user, so it may hand the right to come forward on.
        _ = AllowSetForegroundWindow(AsfwAny);
        if (EventWaitHandle.TryOpenExisting(ComeForwardName, out var comeForward))
        {
            using (comeForward) comeForward.Set();
        }
    }

    /// <summary>Calls <paramref name="comeForward"/> (on a background thread) each time HASI is started again.</summary>
    public void Listen(Action comeForward)
    {
        var thread = new Thread(() =>
        {
            WaitHandle[] handles = [_comeForward, _stop];
            while (WaitHandle.WaitAny(handles) == 0) comeForward();
        })
        { IsBackground = true, Name = "HASI single instance" };
        thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread any more; Windows releases it when the process ends.
        }
        _mutex.Dispose();
        _comeForward.Dispose();
        _stop.Dispose();
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}

/// <summary>
/// "Pornire automată": the per-user Run key, so no administrator rights are needed. When HASI is installed it
/// points to Velopack's launcher next to the <c>current</c> folder, a path that survives updates (ADR 0007).
/// </summary>
internal sealed class RegistryAutostart(IDiagnosticLog log) : Hasi.Core.Settings.IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HASI";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                ErrorLog.Write(ex, "reading autostart");
                return false;
            }
        }
    }

    /// <summary><c>…\HASI.App\hasi.exe</c> when installed (running from <c>…\HASI.App\current</c>), else this exe.</summary>
    private static string StartPath()
    {
        var exe = Environment.ProcessPath!;
        var folder = System.IO.Path.GetDirectoryName(exe)!;
        if (!string.Equals(System.IO.Path.GetFileName(folder), "current", StringComparison.OrdinalIgnoreCase)) return exe;
        var launcher = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folder)!, System.IO.Path.GetFileName(exe));
        return System.IO.File.Exists(launcher) ? launcher : exe;
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            var target = StartPath();
            if (enabled) key.SetValue(ValueName, $"\"{target}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            log.Write("AUTOSTART", enabled ? $"on: {target}" : "off");
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            ErrorLog.Write(ex, "changing autostart");
            return false;
        }
    }
}
