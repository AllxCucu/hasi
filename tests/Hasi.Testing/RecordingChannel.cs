using Hasi.Core.Output;

namespace Hasi.Testing;

/// <summary>
/// Records everything presented, in order. Also behaves like a speech engine that never finishes by itself: each
/// announcement with something to say stays pending until a test finishes it (<see cref="FinishSpeech"/>); a cancel
/// (any key) reports everything pending as Cancelled, and an announcement with nothing to say is Completed at once.
/// </summary>
public sealed class RecordingChannel : IOutputChannel
{
    private readonly List<Announcement> _announcements = [];
    private readonly List<Announcement> _pending = [];

    public IReadOnlyList<Announcement> Announcements => _announcements;

    /// <summary>Announcements being "spoken": not finished, cancelled or failed yet, oldest first.</summary>
    public IReadOnlyList<Announcement> PendingSpeech => _pending;

    public int CancelCount { get; private set; }

    /// <summary>Raised synchronously, like <see cref="Hasi.Core.Speech.SpeechOutputChannel.Finished"/>.</summary>
    public event EventHandler<SpeechFinished>? Finished;

    public void Present(Announcement announcement)
    {
        _announcements.Add(announcement);
        if (string.IsNullOrWhiteSpace(announcement.SpokenText)) Report(announcement, SpeechOutcome.Completed);
        else _pending.Add(announcement);
    }

    public void Cancel()
    {
        CancelCount++;
        while (_pending.Count > 0) FinishSpeech(SpeechOutcome.Cancelled);
    }

    /// <summary>Ends the oldest pending announcement with <paramref name="outcome"/>; false if none is pending.</summary>
    public bool FinishSpeech(SpeechOutcome outcome = SpeechOutcome.Completed)
    {
        if (_pending.Count == 0) return false;
        var oldest = _pending[0];
        _pending.RemoveAt(0);
        Report(oldest, outcome);
        return true;
    }

    private void Report(Announcement announcement, SpeechOutcome outcome) =>
        Finished?.Invoke(this, new SpeechFinished(announcement.Id, outcome));
}
