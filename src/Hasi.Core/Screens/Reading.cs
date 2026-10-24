using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>A message whose end the screen wants to know (continuous reading), with the screen's own token.</summary>
public sealed record TrackedSpeech(Utterance Utterance, long Token);

/// <summary>
/// A screen told when its tracked messages end (<see cref="ScreenResult.SayTracked"/>). The report reaches the screen
/// on the shell's thread, only while the screen is still open.
/// </summary>
public interface ISpeechListener
{
    ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context);
}

/// <summary>The shell's event loop, for work that finishes on another thread or later (implemented by the application).</summary>
public interface IEventLoop
{
    /// <summary>Runs <paramref name="action"/> on the shell's thread, soon.</summary>
    void Post(Action action);

    /// <summary>Runs <paramref name="action"/> on the shell's thread after <paramref name="delay"/>, unless disposed first.</summary>
    IDisposable PostAfter(TimeSpan delay, Action action);

    /// <summary>Starts background work off the shell's thread (the thread pool); a test loop runs it when the test chooses.</summary>
    void StartWork(Action work) => _ = Task.Run(work);
}

/// <summary>A long operation started with <see cref="Modules.IShellServices.RunInBackground"/>.</summary>
public abstract class BackgroundJob
{
    /// <summary>True until the job is cancelled or its outcome has reached the shell's thread.</summary>
    public abstract bool IsRunning { get; }

    /// <summary>Asks the work to stop; its result, if any arrives, is dropped.</summary>
    public abstract void Cancel();
}
