using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Hasi.Documents.Tests;

using Hasi.Core.Documents;

/// <summary>
/// PDFs made in the test, with PdfPig's builder and the standard Helvetica font (which has no ș, ț, ă: those are
/// tested with <see cref="RawPdf"/> and a font with its own character table). Real Romanian PDFs (fonts embedded
/// by Word, scanned pages with OCR, broken fonts) are tested by hand with the tester's files.
/// </summary>
public sealed class PdfDocumentTests : TempFolder
{
    /// <summary>A 1×1 pixel PNG: the "scan" on an image-only page.</summary>
    private static readonly byte[] Pixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private const string Helvetica = "/Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding";

    private readonly DocumentFormats _formats = new();

    private string Write(string name, byte[] bytes)
    {
        var path = PathOf(name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private Document LoadOk(string path)
    {
        var result = _formats.Load(path);
        Assert.Equal(DocumentProblem.None, result.Problem);
        Assert.NotNull(result.Document);
        Assert.Equal(DocumentFormat.Pdf, result.Document.Format);
        Assert.False(result.Document.MadeByHasi);
        return result.Document;
    }

    /// <summary>Writes <paramref name="lines"/> one under the other, 14 points apart, from (x, y) downwards.</summary>
    private static void Lines(PdfPageBuilder page, PdfDocumentBuilder.AddedFont font, double x, double y, params string[] lines)
    {
        for (var i = 0; i < lines.Length; i++) page.AddText(lines[i], 11, new PdfPoint(x, y - (i * 14)), font);
    }

    [Fact]
    public void Paragraphs_come_page_by_page_with_their_page_number()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        Lines(builder.AddPage(PageSize.A4), font, 72, 760, "Prima pagina incepe aici.");
        Lines(builder.AddPage(PageSize.A4), font, 72, 760, "A doua pagina are alt text.");
        Lines(builder.AddPage(PageSize.A4), font, 72, 760, "Ultima pagina.");

        var document = LoadOk(Write("trei.pdf", builder.Build()));

        Assert.Equal(3, document.PageCount);
        Assert.Equal(
            [
                new DocumentParagraph("Prima pagina incepe aici.", Page: 1),
                new DocumentParagraph("A doua pagina are alt text.", Page: 2),
                new DocumentParagraph("Ultima pagina.", Page: 3),
            ],
            document.Paragraphs);
        Assert.Empty(document.ScannedPages);
        Assert.False(document.LooksGarbled);
        Assert.Empty(document.ChapterTitles);
    }

    [Fact]
    public void The_lines_of_a_block_are_one_paragraph_and_a_word_split_at_the_line_end_is_joined()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        Lines(page, font, 72, 760, "Acesta este un cuvan-", "tul cel lung, scris intr-", "un singur paragraf.");
        Lines(page, font, 72, 600, "Al doilea paragraf, departe de primul.");

        var document = LoadOk(Write("rupt.pdf", builder.Build()));

        Assert.Equal(
            ["Acesta este un cuvantul cel lung, scris intr-un singur paragraf.", "Al doilea paragraf, departe de primul."],
            document.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void Two_columns_are_read_column_by_column()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Ziarul de azi", 20, new PdfPoint(220, 780), font);
        Lines(page, font, 50, 720,
            "Stanga unu: vremea va fi", "frumoasa in toata tara,", "cu soare si cer senin", "pana duminica seara.");
        Lines(page, font, 320, 720,
            "Dreapta unu: la Cluj se", "deschide un muzeu nou,", "cu intrare libera in", "prima saptamana.");

        var document = LoadOk(Write("coloane.pdf", builder.Build()));

        Assert.Equal(
            [
                "Ziarul de azi",
                "Stanga unu: vremea va fi frumoasa in toata tara, cu soare si cer senin pana duminica seara.",
                "Dreapta unu: la Cluj se deschide un muzeu nou, cu intrare libera in prima saptamana.",
            ],
            document.Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void A_page_with_only_an_image_is_scanned_and_an_empty_page_is_not()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        Lines(builder.AddPage(PageSize.A4), font, 72, 760, "Pagina cu text.");
        builder.AddPage(PageSize.A4).AddPng(Pixel, new PdfRectangle(50, 50, 545, 792));
        builder.AddPage(PageSize.A4);
        var scannedWithNumber = builder.AddPage(PageSize.A4);
        scannedWithNumber.AddPng(Pixel, new PdfRectangle(50, 80, 545, 792));
        scannedWithNumber.AddText("4", 10, new PdfPoint(297, 40), font);

        var document = LoadOk(Write("scanat.pdf", builder.Build()));

        Assert.Equal(4, document.PageCount);
        Assert.Equal([2, 4], document.ScannedPages);
        // The page number of a scanned page is still text: it is kept.
        Assert.Equal([new DocumentParagraph("Pagina cu text.", Page: 1), new DocumentParagraph("4", Page: 4)],
            document.Paragraphs);
    }

    [Fact]
    public void An_image_with_text_on_the_page_is_not_a_scan()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddPng(Pixel, new PdfRectangle(50, 400, 300, 700));
        Lines(page, font, 72, 300, "Sub poza scrie ceva.");

        Assert.Empty(LoadOk(Write("poza.pdf", builder.Build())).ScannedPages);
    }

    [Fact]
    public void Romanian_diacritics_from_a_font_with_its_own_character_table_are_read()
    {
        const string toUnicode = """
            /CIDInit /ProcSet findresource begin
            12 dict begin
            begincmap
            /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
            /CMapName /Adobe-Identity-UCS def
            /CMapType 2 def
            1 begincodespacerange
            <00> <FF>
            endcodespacerange
            1 beginbfrange
            <20> <7E> <0020>
            endbfrange
            8 beginbfchar
            <80> <0219>
            <81> <021B>
            <82> <0218>
            <83> <021A>
            <84> <0103>
            <85> <0102>
            <86> <00E2>
            <87> <00EE>
            endbfchar
            endcmap
            CMapName currentdict /CMapResource defineresource pop
            end
            end
            """;
        // A simple TrueType font, not embedded. PdfPig ignores the ToUnicode table of a font without a descriptor.
        var font = "/Type /Font /Subtype /TrueType /BaseFont /Arial /Encoding /WinAnsiEncoding /FirstChar 32 /LastChar 135 " +
            $"/Widths [{string.Join(' ', Enumerable.Repeat(556, 104))}] " +
            "/FontDescriptor << /Type /FontDescriptor /FontName /Arial /Flags 32 /FontBBox [0 -200 1000 900] " +
            "/ItalicAngle 0 /Ascent 900 /Descent -200 /CapHeight 700 /StemV 80 >>";
        var pdf = RawPdf.Build(@"BT /F1 12 Tf 72 700 Td (\202tiin\201\204 \200i \201ar\204, \207n m\206n\204 \205 \203) Tj ET",
            font, toUnicode);

        var document = LoadOk(Write("diacritice.pdf", pdf));

        Assert.Equal([new DocumentParagraph("Știință și țară, în mână Ă Ț", Page: 1)], document.Paragraphs);
        Assert.False(document.LooksGarbled);
    }

    [Fact]
    public void Text_from_a_font_without_a_correct_character_table_looks_garbled()
    {
        // Every letter maps to a private-use character, as with fonts whose character table is missing or wrong.
        var differences = string.Join(" ", Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26))
            .Select(code => $"{code} /uniE{code:X3}"));
        var font = "/Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Type /Encoding " +
            $"/BaseEncoding /WinAnsiEncoding /Differences [{differences}] >>";
        var pdf = RawPdf.Build("BT /F1 12 Tf 72 700 Td (Acesta pare un text normal, dar nu este deloc) Tj ET", font);

        var document = LoadOk(Write("stricat.pdf", pdf));

        Assert.True(document.LooksGarbled);
        Assert.Empty(document.ScannedPages);
    }

    [Fact]
    public void A_pdf_that_needs_a_password_is_protected()
    {
        var encrypt = "/Filter /Standard /V 1 /R 2 /P -44 " +
            "/O <" + new string('A', 64) + "> /U <" + new string('B', 64) + ">";
        var pdf = RawPdf.Build("BT /F1 12 Tf 72 700 Td (Secret) Tj ET", Helvetica, encrypt: encrypt);

        var result = _formats.Load(Write("parola.pdf", pdf));

        Assert.Equal(DocumentProblem.Protected, result.Problem);
        Assert.Null(result.Document);
    }

    [Theory]
    [InlineData("nu este un pdf")]
    [InlineData("%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R")]
    [InlineData("")]
    public void A_damaged_pdf_is_damaged(string content)
    {
        var path = PathOf("stricat.pdf");
        File.WriteAllText(path, content);

        var result = _formats.Load(path);

        Assert.Equal(DocumentProblem.Damaged, result.Problem);
        Assert.Null(result.Document);
    }

    [Fact]
    public void A_pdf_cut_in_half_is_damaged_or_read_without_throwing()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        Lines(builder.AddPage(PageSize.A4), font, 72, 760, "Text care se pierde.");
        var bytes = builder.Build();

        var result = _formats.Load(Write("jumatate.pdf", bytes[..(bytes.Length / 2)]));

        Assert.True(result.Problem is DocumentProblem.Damaged or DocumentProblem.None);
    }

    [Theory]
    [InlineData(new[] { "Un cuvân-", "tul" }, "Un cuvântul")]
    [InlineData(new[] { "s-a dus într-", "un loc" }, "s-a dus într-un loc")]
    [InlineData(new[] { "dă-", "mi cartea" }, "dă-mi cartea")]
    [InlineData(new[] { "Cluj-", "Napoca" }, "Cluj-Napoca")]
    [InlineData(new[] { "anii 1990-", "2000" }, "anii 1990-2000")]
    [InlineData(new[] { "linie - ", "alta" }, "linie - alta")]
    [InlineData(new[] { "  multe   spații ", "", " aici " }, "multe spații aici")]
    [InlineData(new[] { "o ﬁșă", "ﬂoare" }, "o fișă floare")]
    [InlineData(new[] { "des­părțire" }, "despărțire")]
    public void Lines_are_joined_into_one_paragraph(string[] lines, string expected)
    {
        Assert.Equal(expected, PdfFiles.JoinLines(lines));
    }

    [Theory]
    [InlineData('a', false)]
    [InlineData('ș', false)]
    [InlineData('Ă', false)]
    [InlineData('7', false)]
    [InlineData('„', false)]
    [InlineData('€', false)]
    [InlineData('̦', false)] // combining comma below
    [InlineData('�', true)]
    [InlineData('', true)]
    [InlineData('\u0001', true)]
    [InlineData('ж', true)]
    [InlineData('α', true)]
    public void Suspicious_characters(char c, bool suspicious)
    {
        Assert.Equal(suspicious, PdfFiles.IsSuspicious(c));
    }

    [Theory]
    [InlineData(100, 30, false)]
    [InlineData(100, 31, true)]
    [InlineData(19, 19, false)] // too little text to judge
    [InlineData(20, 7, true)]
    public void Garbled_means_over_30_percent_suspicious(int characters, int suspicious, bool garbled)
    {
        Assert.Equal(garbled, PdfFiles.LooksGarbled(characters, suspicious));
    }
}
