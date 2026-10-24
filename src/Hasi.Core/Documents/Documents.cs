namespace Hasi.Core.Documents;

public enum DocumentFormat
{
    /// <summary>Plain text, read as UTF-8 (or Windows-1250 when not valid UTF-8), always written as UTF-8.</summary>
    Text,

    /// <summary>Word document (.docx), Office Open XML.</summary>
    Word,

    /// <summary>PDF, read only: its text, page by page.</summary>
    Pdf,

    /// <summary>EPUB e-book, read only: its chapters in reading order.</summary>
    Epub,
}

/// <param name="HeadingLevel">0 for a normal paragraph, 1–6 for a heading.</param>
/// <param name="Page">1-based page of a PDF; 0 when the format has no pages.</param>
/// <param name="Chapter">1-based chapter of an EPUB; 0 when the format has no chapters.</param>
public sealed record DocumentParagraph(string Text, int HeadingLevel = 0, int Page = 0, int Chapter = 0);

/// <summary>
/// What HASI keeps of a document: its paragraphs, in reading order. Module 3 (reading documents) adds pages and
/// chapters.
/// </summary>
/// <param name="MadeByHasi">The document was written by HASI, so saving over it loses nothing.</param>
public sealed record Document(IReadOnlyList<DocumentParagraph> Paragraphs, DocumentFormat Format, bool MadeByHasi)
{
    /// <summary>Number of pages of a PDF; 0 for other formats.</summary>
    public int PageCount { get; init; }

    /// <summary>Chapter titles of an EPUB, in reading order (index 0 = chapter 1); empty for other formats.</summary>
    public IReadOnlyList<string> ChapterTitles { get; init; } = [];

    /// <summary>1-based PDF pages that have no text (scanned images).</summary>
    public IReadOnlyList<int> ScannedPages { get; init; } = [];

    /// <summary>The text looks broken (letters without meaning, from fonts without a correct character table).</summary>
    public bool LooksGarbled { get; init; }
}

public enum DocumentProblem
{
    None,
    NotFound,
    TooLarge,
    Protected,
    Damaged,
    NoAccess,
    DiskFull,
    Unsupported,
}

public sealed record DocumentLoadResult(Document? Document, DocumentProblem Problem = DocumentProblem.None);

/// <summary>Reading and writing the formats HASI supports. Implemented in <c>Hasi.Documents</c>, pure .NET.</summary>
public interface IDocumentFormats
{
    /// <summary>The format of <paramref name="path"/> by its extension, or null when HASI cannot open it.</summary>
    DocumentFormat? FormatOf(string path);

    /// <summary>The extension HASI writes for <paramref name="format"/>, with the dot (".txt", ".docx").</summary>
    string Extension(DocumentFormat format);

    DocumentLoadResult Load(string path);

    /// <summary>Writes atomically (a temporary file next to the target, then a replace). Never throws for I/O problems.</summary>
    DocumentProblem Save(string path, IReadOnlyList<DocumentParagraph> paragraphs, DocumentFormat format);
}
