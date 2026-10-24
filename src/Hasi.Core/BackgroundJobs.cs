using Hasi.Core.Diagnostics;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Core;

/// <summary>
/// The shell's background jobs (<see cref="Modules.IShellServices.RunInBackground"/>): the work runs off the shell's
/// thread, its outcome comes back through the event loop and is applied only while the screen that asked is current.
/// </summary>
internal sealed class BackgroundJobs(IEventLoop loop, IDiagnosticLog log, Shell shell)
{
    /// <summary>How long a job runs before its "please wait" notice is said.</summary>
    public static readonly TimeSpan SlowAfter = TimeSpan.FromSeconds(1);

    private readonly List<Job> _running = [];
    private int _lastId;

    public BackgroundJob Run<T>(IScreen owner, Func<CancellationToken, T> work, Func<T, IScreenContext, ScreenResult> onDone,
        Utterance? slowNotice)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(onDone);

        var job = new Job(++_lastId, owner, log);
        _running.Add(job);
        log.Write("JOB", $"{job.Id} started");
        if (slowNotice is not null)
        {
            job.SlowTimer = loop.PostAfter(SlowAfter, () =>
            {
                if (job.IsRunning && IsCurrent(owner)) shell.Show(ScreenResult.Say(slowNotice), UtterancePriority.Queue);
            });
        }

        var token = job.Token;
        loop.StartWork(() =>
        {
            var result = default(T)!;
            Exception? error = null;
            try
            {
                result = work(token);
            }
#pragma warning disable CA1031 // Whatever the work throws is reported on the shell's thread, never lost on a pool thread.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                error = ex;
            }
            loop.Post(() => Finish(job, result, error, onDone));
        });
        return job;
    }

    /// <summary>After the stack changed: jobs whose screen is no longer open are cancelled.</summary>
    public void CancelLeft(IReadOnlyList<IScreen> stack)
    {
        foreach (var job in _running.Where(j => j.IsRunning && !stack.Contains(j.Owner)).ToList())
        {
            job.Cancel("screen closed");
        }
    }

    private void Finish<T>(Job job, T result, Exception? error, Func<T, IScreenContext, ScreenResult> onDone)
    {
        var cancelled = !job.IsRunning;
        _running.Remove(job);
        job.End();
        if (cancelled)
        {
            log.Write("JOB", $"{job.Id} dropped: cancelled");
        }
        else if (error is not null)
        {
            // Only the type: the message may name a private document.
            log.Write("JOB", $"{job.Id} failed {error.GetType().Name}");
        }
        else if (!IsCurrent(job.Owner))
        {
            log.Write("JOB", $"{job.Id} dropped: screen not current");
        }
        else
        {
            log.Write("JOB", $"{job.Id} finished");
            shell.Show(onDone(result, shell));
        }
    }

    private bool IsCurrent(IScreen owner) => shell.Stack.Count > 0 && ReferenceEquals(shell.Current, owner);

    /// <summary>One job; its state changes only on the shell's thread.</summary>
    private sealed class Job(int id, IScreen owner, IDiagnosticLog log) : BackgroundJob
    {
        private readonly CancellationTokenSource _cancellation = new();
        private bool _cancelled;
        private bool _ended;

        public int Id { get; } = id;

        public IScreen Owner { get; } = owner;

        public CancellationToken Token => _cancellation.Token;

        public IDisposable? SlowTimer { get; set; }

        public override bool IsRunning => !_cancelled && !_ended;

        public override void Cancel() => Cancel("asked");

        public void Cancel(string reason)
        {
            if (!IsRunning) return;
            _cancelled = true;
            SlowTimer?.Dispose();
            log.Write("JOB", $"{Id} cancelled: {reason}");
            _cancellation.Cancel();
        }

        public void End()
        {
            _ended = true;
            SlowTimer?.Dispose();
            _cancellation.Dispose();
        }
    }
}

/// <summary>
/// The loop a shell uses when the host gives none: posted actions run at once, delayed ones never, and background work
/// runs on the calling thread. Everything stays on one thread, as in the tests that never start a job.
/// </summary>
internal sealed class ImmediateEventLoop : IEventLoop
{
    public void Post(Action action) => action();

    public IDisposable PostAfter(TimeSpan delay, Action action) => Never.Instance;

    public void StartWork(Action work) => work();

    private sealed class Never : IDisposable
    {
        public static Never Instance { get; } = new();

        public void Dispose() { }
    }
}
