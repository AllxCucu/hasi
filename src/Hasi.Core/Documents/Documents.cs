namespace Hasi.Core.Documents;

public enum DocumentFormat
{
    /// <summary>Plain text, read as UTF-8 (or Windows-1250 when not valid UTF-8), always written as UTF-8.</summary>
    Text,

    /// <summary>Word document (.docx), Office Open XML.</summary>
    Word,
}

/// <param name="HeadingLevel">0 for a normal paragraph, 1–6 for a heading.</param>
public sealed record DocumentParagraph(string Text, int HeadingLevel = 0);

/// <summary>
/// What HASI keeps of a document: its paragraphs, in reading order. Module 3 (reading documents) adds pages and
/// chapters.
/// </summary>
/// <param name="MadeByHasi">The document was written by HASI, so saving over it loses nothing.</param>
public sealed record Document(IReadOnlyList<DocumentParagraph> Paragraphs, DocumentFormat Format, bool MadeByHasi);

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
