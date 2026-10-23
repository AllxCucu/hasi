using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core.Diagnostics;

/// <summary>What happened when the user asked to copy the diagnostic log to the Desktop.</summary>
public enum LogCopyResult
{
    Copied,

    /// <summary>Diagnostic mode was never on, so there is no log yet.</summary>
    NothingToCopy,

    Failed,
}

/// <summary>Diagnostic mode as the user controls it: from Settings → Diagnostic or with Ctrl+Shift+D.</summary>
public interface IDiagnosticsService
{
    bool IsOn { get; }

    /// <summary>Starts a new session file. False when it cannot be written (diagnostic mode stays off).</summary>
    bool TurnOn();

    void TurnOff();

    /// <summary>Copies the current (or the most recent) session log, with the error log, to the Desktop.</summary>
    LogCopyResult CopyToDesktop();
}

/// <summary>The spoken answers, shared by the global key and the Diagnostic menu.</summary>
public static class DiagnosticsMessages
{
    /// <summary>Switches diagnostic mode and says the new state (or why it could not start).</summary>
    public static Utterance Switch(IDiagnosticsService diagnostics, ILocalizer localizer, bool on)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(localizer);
        if (!on)
        {
            diagnostics.TurnOff();
            return localizer.Get("diagnostic.mode.turnedOff");
        }
        return diagnostics.TurnOn()
            ? localizer.Get("diagnostic.mode.turnedOn")
            : new Utterance(localizer.Get("diagnostic.mode.failed"), Earcon: Earcon.Error);
    }

    public static Utterance CopyToDesktop(IDiagnosticsService diagnostics, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(localizer);
        return diagnostics.CopyToDesktop() switch
        {
            LogCopyResult.Copied => localizer.Get("diagnostic.copyLog.done"),
            LogCopyResult.NothingToCopy => localizer.Get("diagnostic.copyLog.nothing"),
            _ => new Utterance(localizer.Get("diagnostic.copyLog.failed"), Earcon: Earcon.Error),
        };
    }
}

/// <summary>
/// The diagnostic log every component holds. It forwards to the session file while diagnostic mode is on
/// and drops everything while it is off, so the mode can change without rebuilding anything.
/// </summary>
public sealed class SwitchableDiagnosticLog : IDiagnosticLog
{
    private volatile IDiagnosticLog _inner = NullDiagnosticLog.Instance;

    public bool IsEnabled => _inner.IsEnabled;

    public void Write(string category, string message) => _inner.Write(category, message);

    /// <summary>Starts writing to <paramref name="log"/>; returns the previous log, for disposal.</summary>
    public IDiagnosticLog Attach(IDiagnosticLog log) => Interlocked.Exchange(ref _inner, log);

    /// <summary>Stops writing; returns the log that was in use, for disposal.</summary>
    public IDiagnosticLog Detach() => Attach(NullDiagnosticLog.Instance);
}
