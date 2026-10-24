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
        var format = FormatOf(path) ?? throw new ArgumentException("Only .txt and .docx.", nameof(path));
        _documents[path] = new Document([.. text.Split('\n').Select(line => new DocumentParagraph(line))], format, madeByHasi);
        Files?.AddFile(path, text.Length);
        return this;
    }

    public Document? Get(string path) => _documents.GetValueOrDefault(path);

    /// <summary>The document's paragraphs joined with '\n', or null when there is none at that path.</summary>
    public string? TextOf(string path) => Get(path) is { } document ? string.Join('\n', document.Paragraphs.Select(p => p.Text)) : null;

    public DocumentFormat? FormatOf(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".TXT" => DocumentFormat.Text,
        ".DOCX" => DocumentFormat.Word,
        _ => null,
    };

    public string Extension(DocumentFormat format) => format == DocumentFormat.Text ? ".txt" : ".docx";

    public DocumentLoadResult Load(string path)
    {
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
