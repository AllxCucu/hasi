using System.IO.Compression;
using System.Text;

namespace Hasi.Documents.Tests;

using Hasi.Core.Documents;

/// <summary>EPUB books made in the test as zip files: container, package (OPF), table of contents, XHTML chapters.</summary>
public sealed class EpubDocumentTests : TempFolder
{
    private readonly DocumentFormats _formats = new();

    private static string Xhtml(string body, string title = "Carte") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head><title>{title}</title><style>p {"{"} margin: 0 {"}"}</style></head>
        <body>{body}</body>
        </html>
        """;

    /// <summary>An EPUB 2 book: the chapters in this order in the spine, the table of contents (NCX) from <paramref name="toc"/>.</summary>
    private string WriteBook(string name, (string File, string Body)[] chapters, (string Title, string File)[] toc,
        Dictionary<string, string>? extraFiles = null)
    {
        var manifest = string.Join("\n", chapters.Select((c, i) =>
            $"""<item id="c{i}" href="{c.File}" media-type="application/xhtml+xml"/>"""));
        var spine = string.Join("\n", chapters.Select((_, i) => $"""<itemref idref="c{i}"/>"""));
        var opf = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Carte de test</dc:title>
                <dc:identifier id="id">hasi-test</dc:identifier>
                <dc:language>ro</dc:language>
              </metadata>
              <manifest>
                <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                {manifest}
              </manifest>
              <spine toc="ncx">
                {spine}
              </spine>
            </package>
            """;
        var points = string.Join("\n", toc.Select((t, i) => $"""
            <navPoint id="n{i}" playOrder="{i + 1}"><navLabel><text>{t.Title}</text></navLabel><content src="{t.File}"/></navPoint>
            """));
        var ncx = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
              <head><meta name="dtb:uid" content="hasi-test"/></head>
              <docTitle><text>Carte de test</text></docTitle>
              <navMap>{points}</navMap>
            </ncx>
            """;

        var files = new Dictionary<string, string>
        {
            ["META-INF/container.xml"] = """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """,
            ["OEBPS/content.opf"] = opf,
            ["OEBPS/toc.ncx"] = ncx,
        };
        foreach (var (file, body) in chapters) files["OEBPS/" + file] = Xhtml(body);
        foreach (var (path, content) in extraFiles ?? []) files[path] = content;
        return WriteZip(name, files);
    }

    private string WriteZip(string name, Dictionary<string, string> files)
    {
        var path = PathOf(name);
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

    private Document LoadOk(string path)
    {
        var result = _formats.Load(path);
        Assert.Equal(DocumentProblem.None, result.Problem);
        Assert.NotNull(result.Document);
        Assert.Equal(DocumentFormat.Epub, result.Document.Format);
        Assert.False(result.Document.MadeByHasi);
        return result.Document;
    }

    [Fact]
    public void Chapters_come_in_reading_order_with_headings_lists_and_diacritics()
    {
        var path = WriteBook("carte.epub",
            [
                ("unu.xhtml", """
                    <h1>Capitolul întâi: Știința</h1>
                    <p>Țara  are   multe
                       râuri și munți.</p>
                    <ul><li>Mureș</li><li>Olt <em>și</em> Jiu</li></ul>
                    <h2>O parte mică</h2>
                    <p>Sfârșit&#160;de&nbsp;capitol &amp; atât.</p>
                    """),
                ("doi.xhtml", """<h1>Capitolul doi</h1><p>Început.</p>"""),
            ],
            [("Întâi", "unu.xhtml"), ("Al doilea", "doi.xhtml")]);

        var document = LoadOk(path);

        Assert.Equal(
            [
                new DocumentParagraph("Capitolul întâi: Știința", 1, Chapter: 1),
                new DocumentParagraph("Țara are multe râuri și munți.", Chapter: 1),
                new DocumentParagraph("Mureș", Chapter: 1),
                new DocumentParagraph("Olt și Jiu", Chapter: 1),
                new DocumentParagraph("O parte mică", 2, Chapter: 1),
                new DocumentParagraph("Sfârșit de capitol & atât.", Chapter: 1),
                new DocumentParagraph("Capitolul doi", 1, Chapter: 2),
                new DocumentParagraph("Început.", Chapter: 2),
            ],
            document.Paragraphs);
        Assert.Equal(["Întâi", "Al doilea"], document.ChapterTitles);
        Assert.Equal(0, document.PageCount);
        Assert.Empty(document.ScannedPages);
    }

    [Fact]
    public void Tables_quotes_and_nested_blocks_are_read_cell_by_cell_and_block_by_block()
    {
        var path = WriteBook("tabel.epub",
            [
                ("tabel.xhtml", """
                    <table>
                      <caption>Prețuri</caption>
                      <tr><th>Produs</th><th>Preț</th></tr>
                      <tr><td>Pâine</td><td>5 lei</td></tr>
                    </table>
                    <blockquote>Un citat scurt.</blockquote>
                    <div>Text liber
                      <p>Paragraf în div.</p>
                      alt text liber
                      <div><span>Div doar cu text</span></div>
                    </div>
                    <script>alert("nu");</script>
                    <p>Înainte <img src="poza.jpg" alt="o poză"/>după<br/>rând nou.</p>
                    <pre>Vers unu
                      Vers doi</pre>
                    """),
            ],
            [("Tabelul", "tabel.xhtml")]);

        var document = LoadOk(path);

        Assert.Equal(
            [
                "Prețuri", "Produs", "Preț", "Pâine", "5 lei", "Un citat scurt.", "Text liber", "Paragraf în div.",
                "alt text liber", "Div doar cu text", "Înainte după rând nou.", "Vers unu", "Vers doi",
            ],
            document.Paragraphs.Select(p => p.Text));
        Assert.All(document.Paragraphs, p => Assert.Equal(1, p.Chapter));
    }

    [Fact]
    public void Documents_without_text_are_not_chapters_and_titles_fall_back_to_the_first_heading_then_empty()
    {
        var path = WriteBook("titluri.epub",
            [
                ("coperta.xhtml", """<div><img src="coperta.jpg" alt="Coperta"/></div>"""),
                ("unu.xhtml", """<p>Fără titlu în cuprins.</p><h2>Titlul din text</h2><p>Text.</p>"""),
                ("doi.xhtml", """<p>Nici cuprins, nici titlu.</p>"""),
                ("trei.xhtml", """<h1>Titlu în text</h1><p>Text.</p>"""),
            ],
            [("Coperta", "coperta.xhtml"), ("Capitolul trei", "trei.xhtml")]);

        var document = LoadOk(path);

        Assert.Equal(["Titlul din text", "", "Capitolul trei"], document.ChapterTitles);
        Assert.Equal([1, 1, 1, 2, 3, 3], document.Paragraphs.Select(p => p.Chapter));
    }

    [Fact]
    public void An_epub_3_book_takes_its_chapter_titles_from_the_navigation_document()
    {
        var path = WriteZip("epub3.epub", new Dictionary<string, string>
        {
            ["META-INF/container.xml"] = """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="pachet.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """,
            ["pachet.opf"] = """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Carte 3</dc:title><dc:identifier id="id">hasi-3</dc:identifier><dc:language>ro</dc:language>
                  </metadata>
                  <manifest>
                    <item id="nav" href="text/nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="a" href="text/a.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine><itemref idref="a"/></spine>
                </package>
                """,
            ["text/nav.xhtml"] = Xhtml("""
                <nav epub:type="toc"><ol><li><a href="a.xhtml">Povestea   bunicii</a></li></ol></nav>
                """),
            ["text/a.xhtml"] = Xhtml("<p>A fost odată.</p>"),
        });

        var document = LoadOk(path);

        Assert.Equal(["Povestea bunicii"], document.ChapterTitles);
        Assert.Equal([new DocumentParagraph("A fost odată.", Chapter: 1)], document.Paragraphs);
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#aes128-cbc")]
    [InlineData("http://www.w3.org/2001/04/xmlenc#aes256-cbc")]
    public void Encrypted_content_is_protected(string algorithm)
    {
        var encryption = $"""
            <?xml version="1.0"?>
            <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container" xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
              <enc:EncryptedData>
                <enc:EncryptionMethod Algorithm="{algorithm}"/>
                <enc:CipherData><enc:CipherReference URI="OEBPS/unu.xhtml"/></enc:CipherData>
              </enc:EncryptedData>
            </encryption>
            """;
        var path = WriteBook("drm.epub", [("unu.xhtml", "<p>Secret.</p>")], [],
            new Dictionary<string, string> { ["META-INF/encryption.xml"] = encryption });

        var result = _formats.Load(path);

        Assert.Equal(DocumentProblem.Protected, result.Problem);
        Assert.Null(result.Document);
    }

    [Fact]
    public void Only_obfuscated_fonts_do_not_protect_the_book()
    {
        const string encryption = """
            <?xml version="1.0"?>
            <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container" xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
              <enc:EncryptedData>
                <enc:EncryptionMethod Algorithm="http://www.idpf.org/2008/embedding"/>
                <enc:CipherData><enc:CipherReference URI="OEBPS/font.otf"/></enc:CipherData>
              </enc:EncryptedData>
            </encryption>
            """;
        var path = WriteBook("font.epub", [("unu.xhtml", "<p>Se citește.</p>")], [],
            new Dictionary<string, string> { ["META-INF/encryption.xml"] = encryption });

        Assert.Equal(["Se citește."], LoadOk(path).Paragraphs.Select(p => p.Text));
    }

    [Theory]
    [InlineData("META-INF/rights.xml")]
    [InlineData("META-INF/license.lcpl")]
    public void A_book_with_drm_rights_is_protected(string drmFile)
    {
        var path = WriteBook("drm.epub", [("unu.xhtml", "<p>Secret.</p>")], [],
            new Dictionary<string, string> { [drmFile] = "<rights/>" });

        Assert.Equal(DocumentProblem.Protected, _formats.Load(path).Problem);
    }

    [Theory]
    [InlineData("nu este o carte")]
    [InlineData("")]
    public void A_file_that_is_not_a_zip_is_damaged(string content)
    {
        var path = PathOf("stricat.epub");
        File.WriteAllText(path, content);

        var result = _formats.Load(path);

        Assert.Equal(DocumentProblem.Damaged, result.Problem);
        Assert.Null(result.Document);
    }

    [Fact]
    public void A_zip_without_a_book_inside_is_damaged()
    {
        var path = WriteZip("gol.epub", new Dictionary<string, string> { ["altceva.txt"] = "nimic" });

        Assert.Equal(DocumentProblem.Damaged, _formats.Load(path).Problem);
    }

    [Fact]
    public void A_chapter_missing_from_the_zip_is_damaged_rather_than_a_crash()
    {
        var path = WriteBook("lipsa.epub", [("unu.xhtml", "<p>Unu.</p>")], []);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) zip.GetEntry("OEBPS/unu.xhtml")!.Delete();

        var result = _formats.Load(path);

        Assert.True(result.Problem is DocumentProblem.Damaged or DocumentProblem.None);
    }
}
