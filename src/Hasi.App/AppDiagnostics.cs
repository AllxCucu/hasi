using System.IO;
using System.Text;
using Hasi.Core.Diagnostics;

namespace Hasi.App;

/// <summary>
/// Diagnostic mode: each time it is turned on, a new session file in <c>%LOCALAPPDATA%\HASI\diagnostic</c>.
/// It is off at every start unless <c>--diagnostic</c> is given, because the log records everything said.
/// </summary>
internal sealed class AppDiagnostics(SwitchableDiagnosticLog log, string header, TimeProvider time) : IDiagnosticsService, IDisposable
{
    /// <summary>At most this much of the error log goes into the copy on the Desktop.</summary>
    private const int MaxErrorLogChars = 256 * 1024;

    private string? _sessionFile;

    public bool IsOn => _sessionFile is not null;

    public bool TurnOn()
    {
        if (IsOn) return true;
        try
        {
            var path = AppPaths.NewDiagnosticSessionFile(time.GetLocalNow());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Shared for reading, so the log can be copied while it is being written.
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            var session = new TextDiagnosticLog(new StreamWriter(stream, new UTF8Encoding(true)), time);
            session.Write("START", header);
            (log.Attach(session) as IDisposable)?.Dispose();
            _sessionFile = path;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "diagnostic mode could not start");
            return false;
        }
    }

    public void TurnOff()
    {
        (log.Detach() as IDisposable)?.Dispose();
        _sessionFile = null;
    }

    /// <summary>
    /// The current session (or the most recent one) plus the end of the error log, in one text file on the
    /// Desktop that opens in Notepad and can be sent as it is.
    /// </summary>
    public LogCopyResult CopyToDesktop()
    {
        try
        {
            var source = _sessionFile ?? MostRecentSession();
            if (source is null) return LogCopyResult.NothingToCopy;
            var name = Path.GetFileNameWithoutExtension(source).Replace("sesiune-", "HASI-jurnal-", StringComparison.Ordinal) + ".txt";
            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name);

            var text = new StringBuilder(ReadShared(source));
            if (File.Exists(ErrorLog.Path))
            {
                var errors = ReadShared(ErrorLog.Path);
                if (errors.Length > MaxErrorLogChars) errors = errors[^MaxErrorLogChars..];
                text.AppendLine().AppendLine("===== errors.log =====").Append(errors);
            }
            File.WriteAllText(target, text.ToString(), new UTF8Encoding(true));
            log.Write("DIAG", $"copied to {target}");
            return LogCopyResult.Copied;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ErrorLog.Write(ex, "copying the diagnostic log");
            return LogCopyResult.Failed;
        }
    }

    public void Dispose() => TurnOff();

    private static string? MostRecentSession()
    {
        if (!Directory.Exists(AppPaths.DiagnosticFolder)) return null;
        return new DirectoryInfo(AppPaths.DiagnosticFolder).GetFiles("sesiune-*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
