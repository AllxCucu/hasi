using System.Globalization;
using System.IO;
using Hasi.Core.Diagnostics;

namespace Hasi.App;

/// <summary>Where HASI keeps its local files. Nothing leaves the computer.</summary>
internal static class AppPaths
{
    public static string LocalData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASI");

#if DEBUG
    /// <summary>Development-only RHVoice copy, outside the folder removed by uninstall.</summary>
    public static string DevRhVoice { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HASI.Dev", "rhvoice");
#endif

    public static string DiagnosticFolder => Path.Combine(LocalData, "diagnostic");

    /// <summary>Roaming, so the settings follow the user to another computer of the same domain.</summary>
    public static string SettingsFile { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HASI", "settings.json");

    public static string ErrorLog => Path.Combine(LocalData, "logs", "errors.log");

    public static string Resources => Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json");

    public static string NewDiagnosticSessionFile(DateTimeOffset now) => Path.Combine(
        DiagnosticFolder,
        $"sesiune-{now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}.log");
}

/// <summary>The always-on error log (<see cref="ErrorLogFile"/>), separate from the optional diagnostic transcript.</summary>
internal static class ErrorLog
{
    private static ErrorLogFile _file = new(AppPaths.ErrorLog, "HASI", TimeProvider.System);

    public static string Path => _file.Path;

    /// <summary>Called once at startup, so every entry says which version and Windows it comes from.</summary>
    public static void Initialize(string header) => _file = new ErrorLogFile(AppPaths.ErrorLog, header, TimeProvider.System);

    public static void Write(Exception? exception, string context = "error") => _file.Write(context, exception);
}
