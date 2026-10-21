using Hasi.Core.Output;

namespace Hasi.Testing;

/// <summary>Records everything presented, in order.</summary>
public sealed class RecordingChannel : IOutputChannel
{
    private readonly List<Announcement> _announcements = [];

    public IReadOnlyList<Announcement> Announcements => _announcements;

    public int CancelCount { get; private set; }

    public void Present(Announcement announcement) => _announcements.Add(announcement);

    public void Cancel() => CancelCount++;
}
