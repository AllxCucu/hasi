namespace Hasi.Core.Output;

/// <summary>
/// A destination for announcements. Speech is the first implementation; the visual display and the
/// diagnostic log are others. A Braille display would be a future channel.
/// </summary>
public interface IOutputChannel
{
    void Present(Announcement announcement);

    /// <summary>Stops presenting the current announcement and drops queued ones.</summary>
    void Cancel();
}

public interface ITextNormalizer
{
    string Normalize(string text);
}

/// <summary>Returns text unchanged. Replaced by the Romanian normalizer in phase 0.4.</summary>
public sealed class PassThroughNormalizer : ITextNormalizer
{
    public string Normalize(string text) => text;
}
