using System.IO.Compression;
using System.Text;

using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Documents;
using Hasi.Modules.Documents;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>Reading documents end to end with a real EPUB book, made in the test in a temporary Documents folder.</summary>
public sealed class DocumentsRealFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hasi-documents-" + Guid.NewGuid().ToString("N"));

    public DocumentsRealFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>An EPUB 2 book with two chapters and its table of contents.</summary>
    private string WriteBook(string name)
    {
        static string Chapter(string body) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Carte</title></head><body>{body}</body></html>
            """;
        var files = new Dictionary<string, string>
        {
            ["META-INF/container.xml"] = """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """,
            ["OEBPS/content.opf"] = """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Carte de test</dc:title>
                    <dc:identifier id="id">hasi-test</dc:identifier>
                    <dc:language>ro</dc:language>
                  </metadata>
                  <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="c1" href="unu.xhtml" media-type="application/xhtml+xml"/>
                    <item id="c2" href="doi.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine toc="ncx"><itemref idref="c1"/><itemref idref="c2"/></spine>
                </package>
                """,
            ["OEBPS/toc.ncx"] = """
                <?xml version="1.0" encoding="utf-8"?>
                <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                  <head><meta name="dtb:uid" content="hasi-test"/></head>
                  <docTitle><text>Carte de test</text></docTitle>
                  <navMap>
                    <navPoint id="n1" playOrder="1"><navLabel><text>Întâi</text></navLabel><content src="unu.xhtml"/></navPoint>
                    <navPoint id="n2" playOrder="2"><navLabel><text>Al doilea</text></navLabel><content src="doi.xhtml"/></navPoint>
                  </navMap>
                </ncx>
                """,
            ["OEBPS/unu.xhtml"] = Chapter("<h1>Capitolul întâi</h1><p>Țara are munți și râuri.</p>"),
            ["OEBPS/doi.xhtml"] = Chapter("<h1>Capitolul doi</h1><p>Început. Apoi sfârșit.</p>"),
        };

        var path = Path.Combine(_folder, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open()))
            writer.Write("application/epub+zip");
        foreach (var (file, content) in files)
        {
            using var stream = zip.CreateEntry(file).Open();
            stream.Write(new UTF8Encoding(false).GetBytes(content));
        }
        return path;
    }

    [Fact]
    public void A_book_is_opened_read_by_chapter_and_reopened_from_recents_at_its_chapter()
    {
        var path = WriteBook("Carte.epub");
        var h = new ShellHarness(documents: new DocumentFormats());
        h.Files.AddPlace(PlaceKind.Documents, _folder).AddFile(path);
        h.StartWithModules(new DocumentsModule());
        h.Press(Key.Enter);

        Assert.Equal(["Documente. 1 opțiune. Selectat: Carte, carte EPUB, 1 din 1.", "Carte. Titlu 1: Capitolul întâi"],
            h.Press(Key.Enter, Key.Enter, Key.Enter).Skip(1));
        Assert.Equal(["Țara are munți și râuri."], h.Press(Key.Down));
        Assert.Equal(["Capitolul 2: Al doilea. Titlu 1: Capitolul doi"], h.Press(Key.PageDown));
        Assert.Equal(["Început. Apoi sfârșit."], h.Press(Key.Down));
        Assert.Equal(["Început.", "Apoi sfârșit."], h.Press(Key.Enter));
        h.Press(Key.Escape);

        Assert.Equal(["Carte. Continui de la capitolul 2. Început. Apoi sfârșit."], h.Press(Key.End, Key.Enter, Key.Enter).Skip(2));
        Assert.Equal("Rândul 4 din 4. Capitolul 2 din 2.",
            Assert.IsType<DocumentReaderScreen>(h.Shell.Current).Reader.Position(h.Shell).Text);
    }
}
