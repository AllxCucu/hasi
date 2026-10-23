using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Hasi.Core.Diagnostics;
using Hasi.Core.Power;

namespace Hasi.App;

/// <summary>
/// The other programs the user has open, roughly as the taskbar shows them: visible, titled, unowned, not tool
/// windows, not hidden by Windows (cloaked). Windows' own shell (taskbar, desktop, Start, folders) is left out:
/// it never holds unsaved work. Only program names are said, never window titles (they may be private).
/// </summary>
internal static class OpenWindows
{
    private const int GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x80;
    private const int DwmwaCloaked = 14;
    private const string StoreAppHost = "ApplicationFrameHost";

    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "SearchApp",
        "TextInputHost", "LockApp", "ShellHost",
    };

    public static IReadOnlyList<OpenProgram> List(IDiagnosticLog log)
    {
        try
        {
            var own = (uint)Environment.ProcessId;
            List<(IntPtr Window, uint Process)> windows = [];
            EnumWindows((window, _) =>
            {
                if (IsProgramWindow(window, own, out var process)) windows.Add((window, process));
                return true;
            }, IntPtr.Zero);

            var names = new Dictionary<uint, string?>();
            List<string> order = [];
            var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var (window, process) in windows)
            {
                if (!names.TryGetValue(process, out var name)) names[process] = name = ProgramName(process);
                // Store apps all live in ApplicationFrameHost; their window title is the app's name.
                if (name == StoreAppHost) name = Title(window);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (counts.TryAdd(name, 1)) order.Add(name);
                else counts[name]++;
            }
            List<OpenProgram> programs = [.. order.Select(n => new OpenProgram(n, counts[n]))];
            log.Write("POWER", $"open programs: {string.Join(", ", programs.Select(p => $"{p.Name} ({p.Windows})"))}");
            return programs;
        }
#pragma warning disable CA1031 // Listing programs is a courtesy: it must never stop a restart.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
            return [];
        }
    }

    private static bool IsProgramWindow(IntPtr window, uint own, out uint process)
    {
        process = 0;
        if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0) return false;
        if (GetWindow(window, GwOwner) != IntPtr.Zero) return false;
        if ((GetWindowLongPtr(window, GwlExStyle).ToInt64() & WsExToolWindow) != 0) return false;
        if (DwmGetWindowAttribute(window, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        _ = GetWindowThreadProcessId(window, out process);
        return process != own && process != 0;
    }

    /// <summary>The program's own description ("Microsoft Edge"), else its file name; null for Windows' shell.</summary>
    private static string? ProgramName(uint id)
    {
        try
        {
            using var process = Process.GetProcessById((int)id);
            if (ShellProcesses.Contains(process.ProcessName)) return null;
            if (process.ProcessName.Equals(StoreAppHost, StringComparison.OrdinalIgnoreCase)) return StoreAppHost;
            try
            {
                var file = process.MainModule?.FileName;
                var description = file is null ? null : FileVersionInfo.GetVersionInfo(file).FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Elevated programs do not let us read their file; the process name is enough.
            }
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null; // The program closed meanwhile.
        }
    }

    private static string Title(IntPtr window)
    {
        var text = new StringBuilder(GetWindowTextLength(window) + 1);
        _ = GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindow(IntPtr window, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
