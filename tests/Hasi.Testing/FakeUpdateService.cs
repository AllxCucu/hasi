using Hasi.Core.Updates;

namespace Hasi.Testing;

/// <summary>Updates without a network: a test decides when a check or a download finishes, and how.</summary>
public sealed class FakeUpdateService : IUpdateService
{
    private Action<UpdateCheck>? _check;
    private Action<int>? _progress;
    private Action<bool>? _download;

    public string CurrentVersion { get; set; } = "0.1.0";

    public int Checks { get; private set; }

    public int Downloads { get; private set; }

    public bool Applied { get; private set; }

    public void Check(Action<UpdateCheck> done)
    {
        Checks++;
        _check = done;
    }

    public void Download(Action<int> progress, Action<bool> done)
    {
        Downloads++;
        _progress = progress;
        _download = done;
    }

    public void ApplyAndRestart() => Applied = true;

    public void FinishCheck(UpdateCheckStatus status, string? version = null) =>
        (_check ?? throw new InvalidOperationException("No check was started."))(new UpdateCheck(status, version));

    public void Progress(int percent) => (_progress ?? throw new InvalidOperationException("No download."))(percent);

    public void FinishDownload(bool ready) => (_download ?? throw new InvalidOperationException("No download."))(ready);
}
