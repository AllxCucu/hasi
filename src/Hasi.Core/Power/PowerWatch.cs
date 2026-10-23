namespace Hasi.Core.Power;

public enum PowerAction
{
    Restart,
    Shutdown,
}

/// <summary>A program with open windows, named before restarting or shutting down.</summary>
/// <param name="Name">What is said, for example "Notepad".</param>
/// <param name="Windows">How many of its windows are open.</param>
public sealed record OpenProgram(string Name, int Windows = 1);

/// <summary>What the user has to be told about a restart or shutdown that has not happened (yet).</summary>
public enum PowerStatus
{
    /// <summary>Nothing new to say.</summary>
    None,

    /// <summary>Windows has not begun closing the session within <see cref="PowerWatch.StartTimeout"/>.</summary>
    NotStarted,

    /// <summary>Windows began closing the session but is still waiting, usually for a program with unsaved changes.</summary>
    Waiting,

    /// <summary>The restart or shutdown was cancelled (usually by the user, on Windows' blocking screen).</summary>
    Cancelled,
}

/// <summary>
/// Follows a restart or shutdown after HASI asked Windows for it. HASI stays open until Windows ends the
/// session, so it can tell the user when nothing happens, when a program blocks it, or when it is cancelled.
/// A pure state machine: the host feeds it Windows' signals and the time.
/// </summary>
public sealed class PowerWatch
{
    /// <summary>How long a restart may take before the user is told what is going on.</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

    /// <summary>While Windows keeps waiting, the explanation is repeated this often.</summary>
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(30);

    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastWaiting;
    private bool _ending;
    private bool _waiting;

    /// <summary>The restart or shutdown being followed, or null.</summary>
    public PowerAction? Active { get; private set; }

    /// <summary>An earlier attempt in this session was blocked or cancelled: forcing may be offered.</summary>
    public bool WasBlocked { get; private set; }

    /// <summary>Windows accepted the request.</summary>
    public void Started(PowerAction action, DateTimeOffset now)
    {
        Active = action;
        _startedAt = now;
        _ending = false;
        _waiting = false;
    }

    /// <summary>Windows has begun closing the session (it asks the programs whether they agree).</summary>
    /// <returns>True the first time for this attempt.</returns>
    public bool SessionEnding()
    {
        if (Active is null || _ending) return false;
        _ending = true;
        return true;
    }

    /// <summary>Windows reported that the session goes on after all.</summary>
    public PowerStatus Cancelled()
    {
        if (Active is null) return PowerStatus.None;
        Active = null;
        WasBlocked = true;
        return PowerStatus.Cancelled;
    }

    public PowerStatus Tick(DateTimeOffset now)
    {
        if (Active is null) return PowerStatus.None;
        if (_waiting)
        {
            if (now - _lastWaiting < RepeatInterval) return PowerStatus.None;
            _lastWaiting = now;
            return PowerStatus.Waiting;
        }
        if (now - _startedAt < StartTimeout) return PowerStatus.None;
        if (!_ending)
        {
            Active = null;
            return PowerStatus.NotStarted;
        }
        _waiting = true;
        WasBlocked = true;
        _lastWaiting = now;
        return PowerStatus.Waiting;
    }
}
