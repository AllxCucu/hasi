namespace Hasi.Core.Documents;

/// <summary>Where the reader is in a <see cref="Document"/>: a paragraph (0-based) and a character offset in it.</summary>
public sealed record ReadingPosition(int Paragraph, int Offset)
{
    public static ReadingPosition Beginning { get; } = new(0, 0);
}
