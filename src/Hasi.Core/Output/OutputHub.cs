using Hasi.Core.Diagnostics;

namespace Hasi.Core.Output;

/// <summary>Fans announcements out to every channel. A failing channel never silences the others.</summary>
public sealed class OutputHub(IEnumerable<IOutputChannel> channels, IDiagnosticLog log)
{
    private readonly IOutputChannel[] _channels = [.. channels];

    public void Present(Announcement announcement)
    {
        foreach (var channel in _channels)
        {
            Guard(channel, () => channel.Present(announcement));
        }
    }

    public void Cancel()
    {
        foreach (var channel in _channels)
        {
            Guard(channel, channel.Cancel);
        }
    }

    private void Guard(IOutputChannel channel, Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031 // One broken output must not take down speech or the display.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            log.Write("ERROR", $"{channel.GetType().Name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
