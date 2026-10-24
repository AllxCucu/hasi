using System.IO.Compression;
using System.Xml.Linq;

using VersOne.Epub;
using VersOne.Epub.Options;

using HasiDocument = Hasi.Core.Documents.Document;

namespace Hasi.Documents;

using Hasi.Core.Documents;

/// <summary>
/// EPUB books, read only. All VersOne.Epub code of HASI lives here; the text of each content document comes from
/// <see cref="XhtmlText"/>. A chapter is a content document of the reading order that has text; its title comes
/// from the book's table of contents, else from its first heading, else it stays empty (the module names it).
/// </summary>
internal static class EpubFiles
{
    /// <summary>A book whose files would take more than this once unpacked is not opened (a "zip bomb").</summary>
    internal const long MaxUnpackedSize = 512L * 1024 * 1024;

    /// <summary>Encryption methods that only hide embedded fonts from copying: the text stays readable.</summary>
    private static readonly HashSet<string> FontObfuscation = new(StringComparer.Ordinal)
    {
        "http://www.idpf.org/2008/embedding",
        "http://ns.adobe.com/pdf/enc#RC",
    };

    /// <summary>Files that only DRM-protected books have: Adobe ADEPT, Apple FairPlay, Readium LCP.</summary>
    private static readonly string[] DrmFiles = ["META-INF/rights.xml", "META-INF/sinf.xml", "META-INF/license.lcpl"];

    private static readonly XNamespace XmlEncryption = "http://www.w3.org/2001/04/xmlenc#";

    public static DocumentLoadResult Read(byte[] bytes)
    {
        try
        {
            if (Check(bytes) is var problem and not DocumentProblem.None) return new DocumentLoadResult(null, problem);

            using var stream = new MemoryStream(bytes, writable: false);
            var options = new EpubReaderOptions(EpubReaderOptionsPreset.RELAXED);
            options.ContentDownloaderOptions.DownloadContent = false;
            using var book = EpubReader.OpenBook(stream, options) ?? throw new InvalidDataException("No book.");

            var titles = NavigationTitles(book);
            var paragraphs = new List<DocumentParagraph>();
            var chapterTitles = new List<string>();
            foreach (var item in book.GetReadingOrder())
            {
                var blocks = XhtmlText.Read(item.ReadContent());
                if (blocks.Count == 0) continue;

                var chapter = chapterTitles.Count + 1;
                chapterTitles.Add(titles.GetValueOrDefault(item.FilePath)
                    ?? blocks.FirstOrDefault(b => b.HeadingLevel > 0)?.Text
                    ?? "");
                paragraphs.AddRange(blocks.Select(b => new DocumentParagraph(b.Text, b.HeadingLevel, Chapter: chapter)));
            }

            return new DocumentLoadResult(new HasiDocument(paragraphs, DocumentFormat.Epub, MadeByHasi: false)
            {
                ChapterTitles = chapterTitles,
            });
        }
        catch (Exception)
        {
            // Not a zip, no package document, invalid XML: the book cannot be read.
            return new DocumentLoadResult(null, DocumentProblem.Damaged);
        }
    }

    /// <summary>DRM, encrypted content and oversized books, found from the zip directory before reading anything.</summary>
    private static DocumentProblem Check(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        if (zip.Entries.Sum(e => e.Length) > MaxUnpackedSize) return DocumentProblem.TooLarge;
        if (DrmFiles.Any(name => Entry(zip, name) is not null)) return DocumentProblem.Protected;
        if (Entry(zip, "META-INF/encryption.xml") is not { } encryption) return DocumentProblem.None;

        using var reader = encryption.Open();
        var methods = XDocument.Load(reader)
            .Descendants(XmlEncryption + "EncryptedData")
            .Select(data => (string?)data.Element(XmlEncryption + "EncryptionMethod")?.Attribute("Algorithm") ?? "");
        return methods.Any(method => !FontObfuscation.Contains(method)) ? DocumentProblem.Protected : DocumentProblem.None;
    }

    private static ZipArchiveEntry? Entry(ZipArchive zip, string name) =>
        zip.Entries.FirstOrDefault(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The first title the table of contents gives each content file, by its path in the book. A broken table of
    /// contents only loses the titles.
    /// </summary>
    private static Dictionary<string, string> NavigationTitles(EpubBookRef book)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            Add(book.GetNavigation() ?? []);
        }
        catch (Exception)
        {
            // The chapters fall back to their first heading.
        }

        return titles;

        void Add(IEnumerable<EpubNavigationItemRef> items)
        {
            foreach (var item in items)
            {
                var path = item.HtmlContentFileRef?.FilePath ?? item.Link?.ContentFilePath;
                var title = XhtmlText.Collapse(item.Title ?? "");
                if (path is not null && title.Length > 0) titles.TryAdd(path, title);
                Add(item.NestedItems);
            }
        }
    }
}
