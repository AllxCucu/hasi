using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

using ExtendedProperties = DocumentFormat.OpenXml.ExtendedProperties;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace Hasi.Documents.Tests;

// Inside the namespace: the enum Hasi.Core.Documents.DocumentFormat must win over Open XML's root namespace DocumentFormat.
using Hasi.Core.Documents;

public sealed class WordDocumentTests : TempFolder
{
    private readonly DocumentFormats _formats = new();

    /// <summary>A .docx as another program would write it: no "HASI" in its properties.</summary>
    private string WriteForeign(string name, IEnumerable<OpenXmlElement> body, Styles? styles = null, string? application = null)
    {
        var path = PathOf(name);
        using var word = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = word.AddMainDocumentPart();
        main.Document = new WordDocument(new Body(body));
        if (styles is not null) main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
        if (application is not null)
        {
            word.AddExtendedFilePropertiesPart().Properties =
                new ExtendedProperties.Properties(new ExtendedProperties.Application(application));
        }

        return path;
    }

    private static Paragraph P(string text, string? styleId = null)
    {
        var paragraph = new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        if (styleId is not null) paragraph.PrependChild(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        return paragraph;
    }

    private static TableCell Cell(params string[] paragraphs) => new(paragraphs.Select(t => P(t)));

    private Core.Documents.Document LoadOk(string path)
    {
        var result = _formats.Load(path);
        Assert.Equal(DocumentProblem.None, result.Problem);
        Assert.NotNull(result.Document);
        Assert.Equal(DocumentFormat.Word, result.Document.Format);
        return result.Document;
    }

    [Fact]
    public void Paragraphs_and_headings_survive_a_save_and_a_load()
    {
        var path = PathOf("scrisoare.docx");
        DocumentParagraph[] written =
        [
            new("Scrisoare către Ștefan", 1),
            new("Bună ziua, dragă prietene. Îți scriu din Brașov."),
            new(""),
            new("  spații la început și la sfârșit  "),
            new("Planuri", 2),
            new("ŞŢ cu sedilă rămân cum sunt scrise."),
            new("Nivelul șase", 6),
        ];

        Assert.Equal(DocumentProblem.None, _formats.Save(path, written, DocumentFormat.Word));
        var loaded = LoadOk(path);

        Assert.Equal(written, loaded.Paragraphs);
        Assert.True(loaded.MadeByHasi);
        Assert.Empty(TemporaryFiles());
    }

    [Fact]
    public void A_document_saved_by_hasi_defines_its_heading_styles_and_a_12_point_normal_text()
    {
        var path = PathOf("stiluri.docx");
        _formats.Save(path, [new("Titlu", 1), new("Text")], DocumentFormat.Word);

        using var word = WordprocessingDocument.Open(path, false);
        var styles = word.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<Style>().ToList();
        Assert.Equal("Heading1", word.MainDocumentPart.Document!.Body!.Elements<Paragraph>().First().ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        for (var level = 1; level <= 6; level++)
        {
            var heading = Assert.Single(styles, s => s.StyleId?.Value == $"Heading{level}");
            Assert.Equal($"heading {level}", heading.StyleName!.Val!.Value);
        }

        var normal = Assert.Single(styles, s => s.StyleId?.Value == "Normal");
        Assert.Equal("24", normal.StyleRunProperties!.FontSize!.Val!.Value);
        Assert.Equal("HASI", word.ExtendedFilePropertiesPart!.Properties!.Application!.Text);
    }

    [Fact]
    public void A_document_saved_by_hasi_is_valid_office_open_xml()
    {
        var path = PathOf("valid.docx");
        _formats.Save(path, [new("Titlu", 1), new("Text\tcu tab"), new(""), new("Sub", 3), new("caractere\u0001de control")],
            DocumentFormat.Word);

        using var word = WordprocessingDocument.Open(path, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2016).Validate(word)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}");

        Assert.Empty(errors);
    }

    [Fact]
    public void A_document_written_by_another_program_is_not_made_by_hasi()
    {
        var withoutProperties = WriteForeign("word.docx", [P("Din Word")]);
        var fromWord = WriteForeign("word2.docx", [P("Din Word")], application: "Microsoft Office Word");

        Assert.False(LoadOk(withoutProperties).MadeByHasi);
        Assert.False(LoadOk(fromWord).MadeByHasi);
        Assert.Equal([new DocumentParagraph("Din Word")], LoadOk(fromWord).Paragraphs);
    }

    [Fact]
    public void Headings_are_found_by_built_in_style_id_or_by_style_name()
    {
        var styles = new Styles(
            new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Titlu1" },
            new Style(new StyleName { Val = "Titlu 2" }) { Type = StyleValues.Paragraph, StyleId = "TitluDoi" },
            new Style(new StyleName { Val = "Citat" }) { Type = StyleValues.Paragraph, StyleId = "Citat" });
        var path = WriteForeign("titluri.docx",
        [
            P("Capitol", "Titlu1"),
            P("Secțiune", "TitluDoi"),
            P("Subsecțiune", "Heading3"),
            P("Un citat", "Citat"),
            P("Stil care lipsește", "Inexistent"),
            P("Text"),
        ], styles);

        Assert.Equal(
            [new("Capitol", 1), new("Secțiune", 2), new("Subsecțiune", 3), new("Un citat"), new("Stil care lipsește"), new DocumentParagraph("Text")],
            LoadOk(path).Paragraphs);
    }

    [Fact]
    public void Runs_are_joined_and_tabs_and_line_breaks_become_spaces()
    {
        var path = WriteForeign("runs.docx",
        [
            new Paragraph(
                new Run(new Text("Nume:")),
                new Run(new TabChar(), new Text("Ion")),
                new Run(new Break(), new Text("Strada Mare")),
                new Run(new Text(" nr. ") { Space = SpaceProcessingModeValues.Preserve }, new Text("5"))),
        ]);

        Assert.Equal([new DocumentParagraph("Nume: Ion Strada Mare nr. 5")], LoadOk(path).Paragraphs);
    }

    [Fact]
    public void Tables_are_read_row_by_row_and_cell_by_cell_in_document_order()
    {
        var table = new Table(
            new TableRow(Cell("A1"), Cell("B1 primul", "B1 al doilea")),
            new TableRow(Cell("A2"), Cell("B2")));
        var path = WriteForeign("tabel.docx", [P("Înainte"), table, P("După")]);

        Assert.Equal(
            ["Înainte", "A1", "B1 primul", "B1 al doilea", "A2", "B2", "După"],
            LoadOk(path).Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void A_damaged_package_is_reported_as_damaged()
    {
        var notZip = PathOf("stricat.docx");
        File.WriteAllText(notZip, "nu este un document Word");
        var truncated = PathOf("trunchiat.docx");
        _formats.Save(truncated, [new("Text destul de lung ca să conteze")], DocumentFormat.Word);
        var bytes = File.ReadAllBytes(truncated);
        File.WriteAllBytes(truncated, bytes[..(bytes.Length / 2)]);
        var empty = PathOf("gol.docx");
        File.WriteAllBytes(empty, []);

        Assert.Equal(DocumentProblem.Damaged, _formats.Load(notZip).Problem);
        Assert.Equal(DocumentProblem.Damaged, _formats.Load(truncated).Problem);
        Assert.Equal(DocumentProblem.Damaged, _formats.Load(empty).Problem);
        Assert.Null(_formats.Load(notZip).Document);
    }

    [Fact]
    public void An_ole_file_encrypted_or_old_doc_is_reported_as_protected()
    {
        var path = PathOf("parola.docx");
        byte[] bytes = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[1024]];
        File.WriteAllBytes(path, bytes);

        var result = _formats.Load(path);

        Assert.Equal(DocumentProblem.Protected, result.Problem);
        Assert.Null(result.Document);
    }
}
