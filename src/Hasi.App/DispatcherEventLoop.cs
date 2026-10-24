using System.Windows.Threading;
using Hasi.Core.Screens;

namespace Hasi.App;

/// <summary>The shell's event loop on the WPF dispatcher: posted actions run on the window's thread.</summary>
internal sealed class DispatcherEventLoop(Dispatcher dispatcher) : IEventLoop
{
    public void Post(Action action) => dispatcher.BeginInvoke(action);

    public IDisposable PostAfter(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
        return new StopTimer(timer);
    }

    private sealed class StopTimer(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
