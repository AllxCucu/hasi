using System.Globalization;
using System.Text;

namespace Hasi.Core.Diagnostics;

/// <summary>
/// The permanent error log (<c>%LOCALAPPDATA%\HASI\logs\errors.log</c>), always on, separate from the optional
/// diagnostic transcript. It never contains what the user typed or heard, only errors. When it grows past
/// <paramref name="maxBytes"/> it is renamed to <c>errors.old.log</c> (replacing the older one) and a new one starts.
/// Never throws: logging an error must never cause another one.
/// </summary>
/// <param name="header">Written on every entry, for example "HASI 0.1.0, Windows 11 26300".</param>
public sealed class ErrorLogFile(string path, string header, TimeProvider time, long maxBytes = 1024 * 1024)
{
    private readonly Lock _lock = new();

    public string Path { get; } = path;

    public string OldPath => System.IO.Path.ChangeExtension(Path, ".old.log");

    /// <param name="context">Where it happened, for example "unhandled UI exception".</param>
    public void Write(string context, Exception? exception = null)
    {
        var entry = Format(time.GetLocalNow(), header, context, exception);
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
                var file = new FileInfo(Path);
                if (file.Exists && file.Length + entry.Length > maxBytes) File.Move(Path, OldPath, overwrite: true);
                File.AppendAllText(Path, entry, Encoding.UTF8);
            }
#pragma warning disable CA1031 // See the class remarks.
            catch
#pragma warning restore CA1031
            {
            }
        }
    }

    /// <summary>
    /// One entry: a line with the time, the context and the version, then the exception (type, message, stack,
    /// inner exceptions) indented, then a blank line.
    /// </summary>
    public static string Format(DateTimeOffset when, string header, string context, Exception? exception)
    {
        var text = new StringBuilder();
        text.Append(when.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" ERROR ").Append(context).Append(" (").Append(header).Append(')').AppendLine();
        if (exception is not null)
        {
            foreach (var line in exception.ToString().Split('\n'))
            {
                text.Append("    ").AppendLine(line.TrimEnd('\r'));
            }
        }
        return text.AppendLine().ToString();
    }
}
