using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>
/// <see cref="Shell.RunInBackground"/>: the outcome reaches the screen that asked only while it is current; leaving it
/// stops the work. The <see cref="ManualEventLoop"/> decides when work runs and when its outcome arrives.
/// </summary>
public class BackgroundJobTests
{
    private const string Done = "Documentul este deschis.";
    private const string Opening = "Se deschide documentul...";

    private sealed record Setup(ShellHarness H, ManualEventLoop Loop, MenuScreen Owner, StringWriter Log)
    {
        public string LogText => Log.ToString();

        public BackgroundJob Run(Func<CancellationToken, string>? work = null, Utterance? slowNotice = null) =>
            H.Shell.RunInBackground(Owner, work ?? (_ => Done), (text, _) => ScreenResult.Say(text), slowNotice);

        public bool Said(string text) => H.Output.Announcements.Any(a => a.Utterance.Text == text);
    }

    private static Setup Started()
    {
        var log = new StringWriter();
        var loop = new ManualEventLoop();
        var h = new ShellHarness(diagnosticLog: new TextDiagnosticLog(log, TimeProvider.System), eventLoop: loop)
            .Start(TestScreens.Fruits());
        var owner = new MenuScreen("Document", [new MenuItem("Pagina 1")]);
        h.Shell.Show(ScreenResult.Push(owner));
        return new Setup(h, loop, owner, log);
    }

    [Fact]
    public void The_result_is_applied_on_the_loop_while_the_owner_is_current()
    {
        var s = Started();
        var job = s.Run(_ => "Gata.");

        s.Loop.RunWork();
        Assert.True(job.IsRunning);
        Assert.False(s.Said("Gata."));

        s.Loop.RunPending();
        Assert.False(job.IsRunning);
        Assert.Equal("Gata.", s.H.LastSaid);
        Assert.Contains("JOB    1 started", s.LogText, StringComparison.Ordinal);
        Assert.Contains("JOB    1 finished", s.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Jobs_are_numbered_in_order()
    {
        var s = Started();
        s.Run();
        s.Run();
        s.Loop.Finish();

        Assert.Contains("JOB    2 started", s.LogText, StringComparison.Ordinal);
        Assert.Contains("JOB    2 finished", s.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_pops_the_owner_and_cancels_its_job_and_the_result_is_dropped()
    {
        var s = Started();
        var job = s.Run();

        s.H.Press(Key.Escape);
        Assert.False(job.IsRunning);
        var said = s.H.Output.Announcements.Count;
        s.Loop.Finish();

        Assert.Equal(said, s.H.Output.Announcements.Count);
        Assert.False(s.Said(Done));
        Assert.Contains("JOB    1 cancelled: screen closed", s.LogText, StringComparison.Ordinal);
        Assert.Contains("JOB    1 dropped: cancelled", s.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_screen_pushed_on_top_does_not_cancel_but_the_result_is_dropped()
    {
        var s = Started();
        var job = s.Run();
        s.H.Shell.Show(ScreenResult.Push(new MenuScreen("Altceva", [new MenuItem("b")])));

        Assert.True(job.IsRunning);
        s.Loop.Finish();
        s.H.Press(Key.Escape); // Back to the owner: the result does not come back later either.
        s.Loop.Finish();

        Assert.False(s.Said(Done));
        Assert.Contains("JOB    1 dropped: screen not current", s.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_drops_the_result_and_signals_the_work()
    {
        var s = Started();
        var signalled = false;
        var job = s.Run(token =>
        {
            signalled = token.IsCancellationRequested;
            return Done;
        });

        job.Cancel();
        Assert.False(job.IsRunning);
        s.Loop.Finish();

        Assert.True(signalled);
        Assert.False(s.Said(Done));
        Assert.Contains("JOB    1 cancelled: asked", s.LogText, StringComparison.Ordinal);
        Assert.Contains("JOB    1 dropped: cancelled", s.LogText, StringComparison.Ordinal);
    }

    public static TheoryData<string> WaysToLeave => ["pop", "replace", "popTo"];

    [Theory]
    [MemberData(nameof(WaysToLeave))]
    public void Leaving_the_owner_signals_the_work_to_stop(string way)
    {
        var s = Started();
        var signalled = false;
        s.Run(token =>
        {
            signalled = token.IsCancellationRequested;
            return Done;
        });

        s.H.Shell.Show(way switch
        {
            "pop" => ScreenResult.Pop(),
            "replace" => ScreenResult.Replace(new MenuScreen("Altceva", [new MenuItem("b")])),
            _ => ScreenResult.PopTo(s.H.Shell.Stack[0]),
        });
        s.Loop.Finish();

        Assert.True(signalled);
        Assert.False(s.Said(Done));
    }

    [Fact]
    public void The_slow_notice_is_said_once_after_a_second_while_the_job_runs()
    {
        var s = Started();
        s.Run(slowNotice: Opening);

        s.Loop.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(s.Said(Opening));
        s.Loop.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Opening, s.H.LastSaid);
        Assert.Equal(UtterancePriority.Queue, s.H.LastAnnouncement!.Utterance.Priority);

        s.Loop.Advance(TimeSpan.FromSeconds(5));
        Assert.Single(s.H.Output.Announcements, a => a.Utterance.Text == Opening);
        s.Loop.Finish();
        Assert.Equal(Done, s.H.LastSaid);
    }

    [Fact]
    public void No_slow_notice_when_the_job_ends_within_a_second()
    {
        var s = Started();
        s.Run(slowNotice: Opening);

        s.Loop.Finish();
        s.Loop.Advance(TimeSpan.FromSeconds(2));

        Assert.False(s.Said(Opening));
        Assert.Equal(Done, s.H.LastSaid);
    }

    [Fact]
    public void No_slow_notice_when_the_owner_is_not_current_or_the_job_was_cancelled()
    {
        var s = Started();
        s.Run(slowNotice: Opening);
        s.H.Shell.Show(ScreenResult.Push(new MenuScreen("Altceva", [new MenuItem("b")])));
        var cancelled = s.Run(slowNotice: "Altă operație...");
        cancelled.Cancel();

        s.Loop.Advance(TimeSpan.FromSeconds(2));

        Assert.False(s.Said(Opening));
        Assert.False(s.Said("Altă operație..."));
    }

    [Fact]
    public void An_exception_is_logged_by_type_only_and_dropped()
    {
        var s = Started();
        var said = s.H.Output.Announcements.Count;
        var job = s.Run(_ => throw new InvalidOperationException("Scrisoare către Ana"));

        s.Loop.Finish();

        Assert.False(job.IsRunning);
        Assert.Equal(said, s.H.Output.Announcements.Count);
        Assert.Contains("JOB    1 failed InvalidOperationException", s.LogText, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", s.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_an_event_loop_the_work_runs_at_once()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        var job = h.Shell.RunInBackground(h.Shell.Current, _ => 2, (n, _) => ScreenResult.Say($"{n} pagini."));

        Assert.False(job.IsRunning);
        Assert.Equal("2 pagini.", h.LastSaid);
    }
}
