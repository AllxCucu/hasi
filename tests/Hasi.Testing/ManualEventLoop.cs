using Hasi.Core.Screens;

namespace Hasi.Testing;

/// <summary>
/// An event loop driven by the test, so background jobs (<see cref="Hasi.Core.Shell.RunInBackground"/>) are deterministic.
/// Nothing runs by itself:
/// <list type="bullet">
/// <item><see cref="RunWork"/> runs the background work started so far, on the test's thread; each finished piece of work
/// posts its outcome to the loop, so the shell has not seen it yet.</item>
/// <item><see cref="RunPending"/> runs what was posted (outcomes reach the shell) and the delayed actions that are due.</item>
/// <item><see cref="Advance"/> moves time forward (the <see cref="ManualTimeProvider"/> too, when given) and runs what is
/// due, for example the "Se deschide documentul..." notice after a second.</item>
/// </list>
/// A job is "still running" until <see cref="RunWork"/> and then <see cref="RunPending"/> are called, so a test can press
/// keys in between (Escape, another screen) and check what happens to the result. <see cref="Finish"/> does both.
/// Pass it to <see cref="ShellHarness"/> (<c>new ShellHarness(eventLoop: new ManualEventLoop())</c>).
/// </summary>
public sealed class ManualEventLoop(ManualTimeProvider? time = null) : IEventLoop
{
    private readonly Lock _lock = new();
    private readonly Queue<Action> _posted = [];
    private readonly Queue<Action> _work = [];
    private readonly List<Delayed> _delayed = [];
    private TimeSpan _ownClock;

    /// <summary>Background work started and not yet run.</summary>
    public int PendingWork
    {
        get
        {
            lock (_lock) return _work.Count;
        }
    }

    /// <summary>Posted actions not yet run.</summary>
    public int Pending
    {
        get
        {
            lock (_lock) return _posted.Count;
        }
    }

    private DateTimeOffset Now => time?.GetUtcNow() ?? DateTimeOffset.UnixEpoch + _ownClock;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_lock) _posted.Enqueue(action);
    }

    public IDisposable PostAfter(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var delayed = new Delayed(Now + delay, action, this);
        lock (_lock) _delayed.Add(delayed);
        return delayed;
    }

    public void StartWork(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_lock) _work.Enqueue(work);
    }

    /// <summary>Runs every piece of background work started so far (and any it starts), on the calling thread.</summary>
    public void RunWork()
    {
        while (TryTake(_work) is { } work) work();
    }

    /// <summary>Runs every posted action (and those they post), then every delayed action that is due.</summary>
    public void RunPending()
    {
        while (true)
        {
            if (TryTake(_posted) is { } posted)
            {
                posted();
                continue;
            }
            Delayed? due;
            lock (_lock)
            {
                due = _delayed.Where(d => d.DueAt <= Now).OrderBy(d => d.DueAt).FirstOrDefault();
                if (due is not null) _delayed.Remove(due);
            }
            if (due is null) return;
            due.Action();
        }
    }

    /// <summary>Moves time forward by <paramref name="elapsed"/>, then runs what is posted and due.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (time is not null) time.Advance(elapsed);
        else _ownClock += elapsed;
        RunPending();
    }

    /// <summary>Runs the background work, then delivers its outcomes to the shell.</summary>
    public void Finish()
    {
        RunWork();
        RunPending();
    }

    private Action? TryTake(Queue<Action> queue)
    {
        lock (_lock) return queue.TryDequeue(out var action) ? action : null;
    }

    private sealed class Delayed(DateTimeOffset dueAt, Action action, ManualEventLoop loop) : IDisposable
    {
        public DateTimeOffset DueAt { get; } = dueAt;

        public Action Action { get; } = action;

        public void Dispose()
        {
            lock (loop._lock) loop._delayed.Remove(this);
        }
    }
}
