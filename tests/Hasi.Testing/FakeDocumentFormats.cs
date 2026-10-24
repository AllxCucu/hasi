using Hasi.Core.Documents;

namespace Hasi.Testing;

/// <summary>
/// Documents in memory: a test adds them, saving keeps them. When tied to a <see cref="FakeFileSystem"/>, each added or
/// saved document also appears there as a file, so browsing and "the name already exists" see it.
/// </summary>
public sealed class FakeDocumentFormats : IDocumentFormats
{
    private readonly Dictionary<string, Document> _documents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where added and saved documents appear as files; the harness ties it to its own disk.</summary>
    public FakeFileSystem? Files { get; set; }

    /// <summary>What <see cref="Load"/> returns for a path, instead of the document.</summary>
    public Dictionary<string, DocumentProblem> LoadProblems { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Simulates a save that fails (disk full, stick removed); None saves.</summary>
    public DocumentProblem SaveProblem { get; set; }

    /// <summary>Every path written, in order.</summary>
    public List<string> Saved { get; } = [];

    /// <summary>Adds a document; its lines become paragraphs. Word documents are "made by another program" unless said.</summary>
    public FakeDocumentFormats Add(string path, string text, bool madeByHasi = false)
    {
        var format = FormatOf(path) is { } known and (DocumentFormat.Text or DocumentFormat.Word)
            ? known
            : throw new ArgumentException("Only .txt and .docx; add PDF and EPUB documents as a Document.", nameof(path));
        _documents[path] = new Document([.. text.Split('\n').Select(line => new DocumentParagraph(line))], format, madeByHasi);
        Files?.AddFile(path, text.Length);
        return this;
    }

    /// <summary>
    /// Adds a document built by the test, of any format: a PDF with its pages (<see cref="Document.PageCount"/>,
    /// <see cref="Document.ScannedPages"/>, <see cref="Document.LooksGarbled"/>) or an EPUB with its chapters.
    /// </summary>
    public FakeDocumentFormats Add(string path, Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[path] = document;
        Files?.AddFile(path, document.Paragraphs.Sum(p => p.Text.Length));
        return this;
    }

    /// <summary>
    /// A PDF in memory: each item is a page, its paragraphs separated by '\n'; null is a scanned page (no text).
    /// </summary>
    public static Document Pdf(params string?[] pages) => new(
        [.. pages.SelectMany((text, i) => text is null
            ? []
            : text.Split('\n').Select(paragraph => new DocumentParagraph(paragraph, Page: i + 1)))],
        DocumentFormat.Pdf, MadeByHasi: false)
    {
        PageCount = pages.Length,
        ScannedPages = [.. pages.Select((text, i) => text is null ? i + 1 : 0).Where(page => page > 0)],
    };

    /// <summary>An EPUB in memory: each chapter starts with its title, as a heading, then its paragraphs.</summary>
    public static Document Epub(params (string Title, string[] Paragraphs)[] chapters) => new(
        [.. chapters.SelectMany((chapter, i) => chapter.Paragraphs
            .Select(text => new DocumentParagraph(text, Chapter: i + 1))
            .Prepend(new DocumentParagraph(chapter.Title, HeadingLevel: 1, Chapter: i + 1)))],
        DocumentFormat.Epub, MadeByHasi: false)
    {
        ChapterTitles = [.. chapters.Select(c => c.Title)],
    };

    public Document? Get(string path) => _documents.GetValueOrDefault(path);

    /// <summary>The document's paragraphs joined with '\n', or null when there is none at that path.</summary>
    public string? TextOf(string path) => Get(path) is { } document ? string.Join('\n', document.Paragraphs.Select(p => p.Text)) : null;

    public DocumentFormat? FormatOf(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".TXT" => DocumentFormat.Text,
        ".DOCX" => DocumentFormat.Word,
        ".PDF" => DocumentFormat.Pdf,
        ".EPUB" => DocumentFormat.Epub,
        _ => null,
    };

    public string Extension(DocumentFormat format) => format switch
    {
        DocumentFormat.Text => ".txt",
        DocumentFormat.Pdf => ".pdf",
        DocumentFormat.Epub => ".epub",
        _ => ".docx",
    };

    /// <summary>How many times <see cref="Load"/> was called.</summary>
    public int Loads { get; private set; }

    public DocumentLoadResult Load(string path)
    {
        Loads++;
        if (LoadProblems.TryGetValue(path, out var problem)) return new DocumentLoadResult(null, problem);
        return _documents.TryGetValue(path, out var document)
            ? new DocumentLoadResult(document)
            : new DocumentLoadResult(null, DocumentProblem.NotFound);
    }

    public DocumentProblem Save(string path, IReadOnlyList<DocumentParagraph> paragraphs, DocumentFormat format)
    {
        if (SaveProblem != DocumentProblem.None) return SaveProblem;
        // As the real one: HASI marks the Word documents it writes as its own.
        _documents[path] = new Document([.. paragraphs], format, MadeByHasi: format == DocumentFormat.Word);
        Saved.Add(path);
        Files?.AddFile(path, paragraphs.Sum(p => p.Text.Length));
        return DocumentProblem.None;
    }
}
