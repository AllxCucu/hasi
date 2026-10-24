using System.Text;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using ExtendedProperties = DocumentFormat.OpenXml.ExtendedProperties;
using HasiDocument = Hasi.Core.Documents.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace Hasi.Documents;

// Inside the namespace: the enum Hasi.Core.Documents.DocumentFormat must win over Open XML's root namespace DocumentFormat.
using Hasi.Core.Documents;

/// <summary>
/// Word documents (.docx). All Open XML SDK code of HASI lives here, so a change of library stays in this file.
/// HASI keeps only the text and the heading levels; formatting, images and comments are not read.
/// </summary>
internal static partial class WordFiles
{
    /// <summary>The application name HASI writes in the document properties, and looks for to know its own files.</summary>
    public const string ApplicationName = "HASI";

    /// <summary>Normal text size in half-points (12 pt).</summary>
    private const string NormalSize = "24";

    private static readonly string[] HeadingSizes = ["32", "28", "26", "24", "24", "24"];

    public static DocumentLoadResult Read(byte[] bytes)
    {
        // Encrypted Office documents and old .doc files are OLE compound files, not zip packages.
        if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xD0, 0xCF, 0x11, 0xE0]))
            return new DocumentLoadResult(null, DocumentProblem.Protected);

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var word = WordprocessingDocument.Open(stream, isEditable: false);
            var body = word.MainDocumentPart?.Document?.Body;
            if (body is null) return new DocumentLoadResult(null, DocumentProblem.Damaged);

            var styleNames = StyleNames(word.MainDocumentPart!.StyleDefinitionsPart);
            var paragraphs = body.Descendants<Paragraph>()
                .Where(p => !p.Ancestors<Paragraph>().Any()) // text boxes inside a paragraph are not read
                .Select(p => new DocumentParagraph(TextOf(p), HeadingLevel(p, styleNames)))
                .ToList();
            var madeByHasi = word.ExtendedFilePropertiesPart?.Properties?.Application?.Text == ApplicationName;
            return new DocumentLoadResult(new HasiDocument(paragraphs, DocumentFormat.Word, madeByHasi));
        }
        catch (Exception)
        {
            // Not a valid package, invalid XML, a part missing: the file cannot be read.
            return new DocumentLoadResult(null, DocumentProblem.Damaged);
        }
    }

    public static void Write(string path, IReadOnlyList<DocumentParagraph> paragraphs)
    {
        using var word = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var main = word.AddMainDocumentPart();
        main.Document = new WordDocument(new Body(paragraphs.Select(ToParagraph)));

        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = CreateStyles();

        var properties = word.AddExtendedFilePropertiesPart();
        properties.Properties = new ExtendedProperties.Properties(new ExtendedProperties.Application(ApplicationName));
    }

    private static Dictionary<string, string> StyleNames(StyleDefinitionsPart? part)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in part?.Styles?.Elements<Style>() ?? [])
        {
            if (style.StyleId?.Value is { } id && style.StyleName?.Val?.Value is { } name) names.TryAdd(id, name);
        }

        return names;
    }

    private static int HeadingLevel(Paragraph paragraph, Dictionary<string, string> styleNames)
    {
        if (paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value is not { } styleId) return 0;
        if (BuiltInHeadingId().Match(styleId) is { Success: true } byId) return int.Parse(byId.Groups[1].ValueSpan);
        if (styleNames.TryGetValue(styleId, out var name) && HeadingName().Match(name) is { Success: true } byName)
            return int.Parse(byName.Groups[1].ValueSpan);
        return 0;
    }

    private static string TextOf(Paragraph paragraph)
    {
        var text = new StringBuilder();
        foreach (var element in paragraph.Descendants())
        {
            if (element is not (Text or TabChar or PositionalTab or Break or CarriageReturn)) continue;
            if (element.Ancestors<Paragraph>().FirstOrDefault() != paragraph) continue;
            text.Append(element is Text t ? t.Text : " ");
        }

        return text.ToString();
    }

    private static Paragraph ToParagraph(DocumentParagraph source)
    {
        var paragraph = new Paragraph();
        if (source.HeadingLevel is >= 1 and <= 6)
        {
            paragraph.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId { Val = HeadingStyleId(source.HeadingLevel) });
        }

        if (source.Text.Length == 0) return paragraph;

        var run = new Run();
        var parts = Clean(source.Text).Split('\t');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) run.AppendChild(new TabChar());
            if (parts[i].Length > 0) run.AppendChild(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        paragraph.AppendChild(run);
        return paragraph;
    }

    /// <summary>Control characters are not allowed in XML; they become spaces (tabs are kept).</summary>
    private static string Clean(string text) =>
        text.Any(c => c < ' ' && c != '\t') ? new string(text.Select(c => c < ' ' && c != '\t' ? ' ' : c).ToArray()) : text;

    private static string HeadingStyleId(int level) => $"Heading{level}";

    private static Styles CreateStyles()
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new FontSize { Val = NormalSize }, new FontSizeComplexScript { Val = NormalSize })),
                new ParagraphPropertiesDefault()),
            new Style(
                new StyleName { Val = "Normal" },
                new PrimaryStyle(),
                new StyleRunProperties(new FontSize { Val = NormalSize }, new FontSizeComplexScript { Val = NormalSize }))
            {
                Type = StyleValues.Paragraph, StyleId = "Normal", Default = true,
            });

        for (var level = 1; level <= 6; level++)
        {
            var size = HeadingSizes[level - 1];
            styles.AppendChild(new Style(
                new StyleName { Val = $"heading {level}" },
                new BasedOn { Val = "Normal" },
                new NextParagraphStyle { Val = "Normal" },
                new PrimaryStyle(),
                new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines { Before = "240", After = "120" },
                    new OutlineLevel { Val = level - 1 }),
                new StyleRunProperties(new Bold(), new FontSize { Val = size }, new FontSizeComplexScript { Val = size }))
            {
                Type = StyleValues.Paragraph, StyleId = HeadingStyleId(level),
            });
        }

        return styles;
    }

    [GeneratedRegex(@"^Heading([1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuiltInHeadingId();

    [GeneratedRegex(@"^(?:heading|titlu)\s*([1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingName();
}
