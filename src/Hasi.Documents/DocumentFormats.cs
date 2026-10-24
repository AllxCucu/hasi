namespace Hasi.Documents;

// Inside the namespace: the enum Hasi.Core.Documents.DocumentFormat must win over Open XML's root namespace DocumentFormat.
using Hasi.Core.Documents;

/// <summary>
/// The formats HASI reads and writes: plain text (<see cref="TextFiles"/>) and Word (<see cref="WordFiles"/>).
/// This class only maps paths and I/O errors to <see cref="DocumentProblem"/>s; the format code lives in those two.
/// </summary>
public sealed class DocumentFormats : IDocumentFormats
{
    /// <summary>Files larger than this are not opened (10 MB).</summary>
    public const long MaxFileSize = 10L * 1024 * 1024;

    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);

    public DocumentFormat? FormatOf(string path) =>
        Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".TXT" => DocumentFormat.Text,
            ".DOCX" => DocumentFormat.Word,
            _ => null,
        };

    public string Extension(DocumentFormat format) => format switch
    {
        DocumentFormat.Text => ".txt",
        DocumentFormat.Word => ".docx",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public DocumentLoadResult Load(string path)
    {
        if (FormatOf(path) is not { } format) return new DocumentLoadResult(null, DocumentProblem.Unsupported);

        byte[] bytes;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new DocumentLoadResult(null, DocumentProblem.NotFound);
            if (info.Length > MaxFileSize) return new DocumentLoadResult(null, DocumentProblem.TooLarge);

            // FileShare.ReadWrite: a document that is open in Word can still be read.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > MaxFileSize) return new DocumentLoadResult(null, DocumentProblem.TooLarge);
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or DriveNotFoundException)
        {
            return new DocumentLoadResult(null, DocumentProblem.NotFound);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return new DocumentLoadResult(null, DocumentProblem.NoAccess);
        }

        return format switch
        {
            DocumentFormat.Text => new DocumentLoadResult(new Document(TextFiles.Read(bytes), DocumentFormat.Text, MadeByHasi: true)),
            _ => WordFiles.Read(bytes),
        };
    }

    public DocumentProblem Save(string path, IReadOnlyList<DocumentParagraph> paragraphs, DocumentFormat format)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        string? temporary = null;
        var replaced = false;
        try
        {
            var full = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(full) ?? throw new DirectoryNotFoundException();
            temporary = Path.Combine(folder, $"~hasi-{Guid.NewGuid():N}.tmp");

            switch (format)
            {
                case DocumentFormat.Text:
                    TextFiles.Write(temporary, paragraphs);
                    break;
                case DocumentFormat.Word:
                    WordFiles.Write(temporary, paragraphs);
                    break;
                default:
                    return DocumentProblem.Unsupported;
            }

            File.Move(temporary, full, overwrite: true);
            replaced = true;
            return DocumentProblem.None;
        }
        catch (UnauthorizedAccessException)
        {
            return DocumentProblem.NoAccess;
        }
        catch (Exception e) when (e is DirectoryNotFoundException or DriveNotFoundException)
        {
            return DocumentProblem.NotFound;
        }
        catch (IOException e) when (e.HResult is ErrorDiskFull or ErrorHandleDiskFull)
        {
            return DocumentProblem.DiskFull;
        }
        catch (IOException)
        {
            return DocumentProblem.NoAccess;
        }
        finally
        {
            if (!replaced && temporary is not null) TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do: the target was not touched.
        }
    }
}
