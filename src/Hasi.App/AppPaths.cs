using System.Globalization;
using System.IO;

namespace Hasi.App;

/// <summary>Where HASI keeps its local files. Nothing leaves the computer.</summary>
internal static class AppPaths
{
    public static string LocalData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASI");

    public static string DiagnosticFolder => Path.Combine(LocalData, "diagnostic");

    public static string ErrorLog => Path.Combine(LocalData, "logs", "errors.log");

    public static string Resources => Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json");

    public static string NewDiagnosticSessionFile(DateTimeOffset now) => Path.Combine(
        DiagnosticFolder,
        $"sesiune-{now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}.log");
}

/// <summary>Always-on error log, separate from the optional diagnostic transcript.</summary>
internal static class ErrorLog
{
    private static readonly Lock Sync = new();

    public static void Write(Exception exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ErrorLog)!);
                File.AppendAllText(AppPaths.ErrorLog,
                    $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
#pragma warning disable CA1031 // Logging an error must never cause another one.
        catch
#pragma warning restore CA1031
        {
        }
    }
}
