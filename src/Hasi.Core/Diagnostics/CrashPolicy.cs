namespace Hasi.Core.Diagnostics;

/// <summary>
/// Decides when an error is serious enough to restart HASI, and when restarting has to stop because it does
/// not help. Pure: the host passes the time and the earlier restarts.
/// </summary>
public static class CrashPolicy
{
    /// <summary>This many unexpected errors on the user's actions within <see cref="BurstWindow"/> are serious.</summary>
    public const int BurstCount = 3;

    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(30);

    /// <summary>At most this many automatic restarts within <see cref="RestartWindow"/>; then HASI gives up.</summary>
    public const int MaxRestarts = 3;

    public static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(10);

    /// <summary>The window becomes unresponsive for this long: HASI is considered frozen.</summary>
    public static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(20);

    /// <summary>True when restarting is still allowed, given the earlier automatic restarts.</summary>
    public static bool MayRestart(IEnumerable<DateTimeOffset> earlierRestarts, DateTimeOffset now) =>
        earlierRestarts.Count(t => now - t < RestartWindow && t <= now) < MaxRestarts;
}

/// <summary>
/// Counts unexpected errors that HASI survived. One is reported to the user and HASI goes on; a burst of
/// them means something is broken for good, and HASI restarts.
/// </summary>
public sealed class ErrorBurst
{
    private readonly Queue<DateTimeOffset> _recent = new();

    /// <summary>Records an error; true when it completes a burst.</summary>
    public bool Add(DateTimeOffset now)
    {
        _recent.Enqueue(now);
        while (_recent.Count > 0 && now - _recent.Peek() >= CrashPolicy.BurstWindow) _recent.Dequeue();
        return _recent.Count >= CrashPolicy.BurstCount;
    }
}
