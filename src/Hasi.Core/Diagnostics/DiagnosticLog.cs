using System.Globalization;

namespace Hasi.Core.Diagnostics;

/// <summary>
/// Session transcript for the tester: keys, commands, screens and everything said.
/// Disabled (<see cref="NullDiagnosticLog"/>) unless the user turns diagnostic mode on.
/// </summary>
public interface IDiagnosticLog
{
    bool IsEnabled { get; }

    void Write(string category, string message);
}

public sealed class NullDiagnosticLog : IDiagnosticLog
{
    public static NullDiagnosticLog Instance { get; } = new();

    public bool IsEnabled => false;

    public void Write(string category, string message) { }
}

/// <summary>Writes lines like <c>14:30:07.010 KEY    Down</c>. Thread-safe.</summary>
public sealed class TextDiagnosticLog(TextWriter writer, TimeProvider time) : IDiagnosticLog, IDisposable
{
    public const string Mask = "•••";

    private readonly Lock _lock = new();

    public bool IsEnabled => true;

    public void Write(string category, string message)
    {
        var timestamp = time.GetLocalNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        lock (_lock)
        {
            writer.WriteLine($"{timestamp} {category,-6} {message}");
            writer.Flush();
        }
    }

    public void Dispose() => writer.Dispose();
}
