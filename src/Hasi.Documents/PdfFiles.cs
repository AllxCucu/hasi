using System.Globalization;
using System.Text;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Exceptions;

using HasiDocument = Hasi.Core.Documents.Document;

namespace Hasi.Documents;

using Hasi.Core.Documents;

/// <summary>
/// PDF, read only. All PdfPig code of HASI lives here (PdfPig is still 0.1.x, so its API may change).
/// HASI keeps the text, page by page: each text block PdfPig's layout analysis finds becomes one paragraph, in
/// reading order, so two-column pages are read column by column. Images, links and forms are not read.
/// </summary>
internal static class PdfFiles
{
    /// <summary>A page with fewer letters than this, but with images, is a scanned page.</summary>
    internal const int MinLettersForText = 5;

    /// <summary>
    /// The text looks garbled when more than this share of its non-space characters is suspicious
    /// (see <see cref="IsSuspicious"/>).
    /// </summary>
    internal const double GarbledShare = 0.30;

    /// <summary>Below this many non-space characters in the whole document, HASI does not judge the text.</summary>
    internal const int MinCharactersToJudge = 20;

    /// <summary>
    /// Romanian words that follow a hyphen ("într-un", "s-a", "dă-mi"): a line that ends in a hyphen before one of
    /// these keeps the hyphen; any other hyphen at the end of a line, between two lower-case letters, splits a word.
    /// Ambiguous on purpose: "fete-" + "le" keeps its hyphen too (rare, and still understandable when heard).
    /// </summary>
    private static readonly HashSet<string> HyphenatedWords = new(StringComparer.Ordinal)
    {
        "a", "ai", "al", "am", "ar", "aș", "ați", "au", "i", "î", "îi", "îl", "îmi", "îți", "l", "le", "lui",
        "m", "mi", "n", "o", "s", "și", "ți", "un", "una",
    };

    public static DocumentLoadResult Read(byte[] bytes)
    {
        try
        {
            var options = new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true };
            using var pdf = PdfDocument.Open(bytes, options);

            var paragraphs = new List<DocumentParagraph>();
            var scanned = new List<int>();
            var characters = 0;
            var suspicious = 0;
            foreach (var page in pdf.GetPages())
            {
                var letters = page.Letters;
                var pageText = string.Concat(letters.Select(l => l.Value));
                if (pageText.Count(char.IsLetter) < MinLettersForText && page.GetImages().Any())
                    scanned.Add(page.Number);

                foreach (var c in pageText)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    characters++;
                    if (IsSuspicious(c)) suspicious++;
                }

                paragraphs.AddRange(ParagraphsOf(letters).Select(text => new DocumentParagraph(text, Page: page.Number)));
            }

            return new DocumentLoadResult(new HasiDocument(paragraphs, DocumentFormat.Pdf, MadeByHasi: false)
            {
                PageCount = pdf.NumberOfPages,
                ScannedPages = scanned,
                LooksGarbled = LooksGarbled(characters, suspicious),
            });
        }
        catch (PdfDocumentEncryptedException)
        {
            // Encrypted with a password the user must type: HASI does not ask for passwords.
            return new DocumentLoadResult(null, DocumentProblem.Protected);
        }
        catch (Exception)
        {
            // Anything PdfPig cannot parse: the file cannot be read.
            return new DocumentLoadResult(null, DocumentProblem.Damaged);
        }
    }

    /// <summary>The page's text blocks in reading order, each with its lines joined.</summary>
    private static IEnumerable<string> ParagraphsOf(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0) return [];
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters).ToList();
        if (words.Count == 0) return [];
        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        return UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .OrderBy(block => block.ReadingOrder)
            .Select(block => JoinLines(block.TextLines.Select(line => line.Text)))
            .Where(text => text.Length > 0);
    }

    internal static bool LooksGarbled(int characters, int suspicious) =>
        characters >= MinCharactersToJudge && suspicious > characters * GarbledShare;

    /// <summary>
    /// Joins the lines of one text block with spaces. A word split at the end of a line ("cuvân-" + "tul") is joined
    /// again; a hyphen that belongs to the text ("într-" + "un", "Cluj-" + "Napoca", "1990-" + "2000") is kept,
    /// without a space. Ligatures (ﬁ, ﬂ) become plain letters, and runs of white space become one space.
    /// </summary>
    internal static string JoinLines(IEnumerable<string> lines)
    {
        var text = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = Clean(raw);
            if (line.Length == 0) continue;
            if (text.Length == 0)
            {
                text.Append(line);
                continue;
            }

            if (text[^1] == '-' && text.Length >= 2 && char.IsLetterOrDigit(text[^2]))
            {
                if (char.IsLower(text[^2]) && char.IsLower(line[0]) && !HyphenatedWords.Contains(FirstWord(line)))
                    text.Length--;
                text.Append(line);
            }
            else
            {
                text.Append(' ').Append(line);
            }
        }

        return text.ToString();
    }

    private static string FirstWord(string line)
    {
        var end = 0;
        while (end < line.Length && char.IsLetter(line[end])) end++;
        return line[..end];
    }

    /// <summary>Soft hyphens and zero-width characters removed, ligatures expanded, white space collapsed, trimmed.</summary>
    private static string Clean(string line)
    {
        var text = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            if (c is '­' or '​' or '‌' or '‍' or '﻿') continue;
            if (c is >= 'ﬀ' and <= 'ﬆ')
            {
                text.Append(c.ToString().Normalize(NormalizationForm.FormKC));
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (text.Length > 0 && text[^1] != ' ') text.Append(' ');
                continue;
            }

            text.Append(c);
        }

        return text.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// A character that does not belong in a Romanian (or other Latin-script) text: a letter of another script,
    /// the replacement character U+FFFD, a private-use character, a control character or a lone surrogate.
    /// Latin letters, digits, combining accents, punctuation and the usual symbols are not suspicious.
    /// </summary>
    internal static bool IsSuspicious(char c)
    {
        if (c == '�') return true;
        switch (char.GetUnicodeCategory(c))
        {
            case UnicodeCategory.PrivateUse:
            case UnicodeCategory.Control:
            case UnicodeCategory.Surrogate:
            case UnicodeCategory.OtherNotAssigned:
                return true;
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
                return !IsLatin(c);
            default:
                return false;
        }
    }

    private static bool IsLatin(char c) =>
        c <= 'ɏ' // Basic Latin, Latin-1, Latin Extended-A and -B
        || c is >= 'Ḁ' and <= 'ỿ' // Latin Extended Additional
        || c is >= 'Ⱡ' and <= 'Ɀ' // Latin Extended-C
        || c is >= '꜠' and <= 'ꟿ' // Latin Extended-D
        || c is >= 'ﬀ' and <= 'ﬆ'; // Latin ligatures (ﬁ, ﬂ)
}
