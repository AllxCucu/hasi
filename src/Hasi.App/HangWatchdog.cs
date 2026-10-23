using System.Diagnostics;
using System.Windows.Threading;
using Hasi.Core.Diagnostics;

namespace Hasi.App;

/// <summary>
/// Notices when the window stops answering (a frozen HASI is silent, and the user cannot tell why).
/// A background thread posts a ping to the window every second; if none is answered for
/// <see cref="CrashPolicy.HangTimeout"/>, the window is considered frozen.
/// Time is counted in one-second waits, not with the clock, so sleep or hibernation never looks like a freeze.
/// </summary>
internal static class HangWatchdog
{
    public static void Start(Dispatcher dispatcher, Action frozen)
    {
        var thread = new Thread(() => Watch(dispatcher, frozen)) { IsBackground = true, Name = "HASI watchdog" };
        thread.Start();
    }

    private static void Watch(Dispatcher dispatcher, Action frozen)
    {
        var limit = (int)CrashPolicy.HangTimeout.TotalSeconds;
        using var answered = new ManualResetEventSlim(false);
        while (!dispatcher.HasShutdownStarted)
        {
            answered.Reset();
            dispatcher.BeginInvoke(DispatcherPriority.Background, answered.Set);
            var waited = 0;
            while (!answered.Wait(TimeSpan.FromSeconds(1)))
            {
                if (dispatcher.HasShutdownStarted) return;
                // A debugger pause, or Windows ending the session, is not a freeze.
                if (Debugger.IsAttached || PowerActions.SessionIsEnding) waited = 0;
                else if (++waited >= limit)
                {
                    frozen();
                    return;
                }
            }
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }
}

/// <summary><c>--test-error crash|hang|ui</c>: a serious error 5 seconds after start, to hear the recovery.</summary>
internal static class TestErrors
{
    public static void Schedule(string kind, Dispatcher dispatcher)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            switch (kind)
            {
                case "crash":
                    new Thread(() => throw new InvalidOperationException("Test error (--test-error crash).")) { IsBackground = true }.Start();
                    break;
                case "hang":
                    Thread.Sleep(Timeout.Infinite);
                    break;
                case "ui":
                    for (var i = 0; i < CrashPolicy.BurstCount; i++)
                    {
                        dispatcher.BeginInvoke(() => throw new InvalidOperationException("Test error (--test-error ui)."));
                    }
                    break;
            }
        };
        timer.Start();
    }
}
