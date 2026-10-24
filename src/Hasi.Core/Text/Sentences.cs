namespace Hasi.Core.Text;

/// <summary>One sentence of a paragraph: from <paramref name="Start"/> up to, not including, <paramref name="End"/>.</summary>
public readonly record struct SentenceSpan(int Start, int End);

/// <summary>A sentence to read aloud: where it starts and ends in the whole text, and what is said.</summary>
public sealed record ReadingSentence(int Start, int End, string Text);

/// <summary>
/// Splits text into sentences for reading aloud. A sentence ends at . ! ? or … (several in a row count as one, closing
/// quotes and brackets stay with it) followed by a space or the end of the line, except after the common Romanian
/// abbreviations ("nr.", "str.", "ș.a.", ...). The end of a paragraph always ends a sentence. Pure logic.
/// </summary>
public static class Sentences
{
    /// <summary>Lower case, without the final period; "ș.a." is compared as "ș.a".</summary>
    private static readonly HashSet<string> Abbreviations = new(StringComparer.Ordinal)
    {
        "nr", "str", "dl", "dna", "ș.a", "etc", "art", "alin", "lit", "pct", "tel", "prof", "dr", "sf", "jud", "bl", "sc", "ap",
    };

    /// <summary>The sentences of one paragraph (a text without line breaks), spaces around them left out.</summary>
    public static IReadOnlyList<SentenceSpan> Split(string paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        var sentences = new List<SentenceSpan>();
        var n = paragraph.Length;
        var i = 0;
        while (true)
        {
            while (i < n && char.IsWhiteSpace(paragraph[i])) i++;
            if (i >= n) break;
            var start = i;
            var end = -1;
            while (i < n)
            {
                if (!IsTerminator(paragraph[i]))
                {
                    i++;
                    continue;
                }
                var terminators = i;
                while (i < n && IsTerminator(paragraph[i])) i++;
                var single = i - terminators == 1 && paragraph[terminators] == '.';
                while (i < n && IsCloser(paragraph[i])) i++;
                if ((i >= n || char.IsWhiteSpace(paragraph[i])) && !(single && IsAbbreviation(paragraph, terminators)))
                {
                    end = i;
                    break;
                }
            }
            if (end < 0)
            {
                end = n;
                while (end > start && char.IsWhiteSpace(paragraph[end - 1])) end--;
            }
            sentences.Add(new SentenceSpan(start, end));
            i = end;
        }
        return sentences;
    }

    /// <summary>
    /// The sentences of <paramref name="text"/> (lines separated by '\n') from the one that contains
    /// <paramref name="position"/> to the end, lazily. A position on the spaces after a sentence, or at the end of a
    /// line, starts with the next sentence.
    /// </summary>
    public static IEnumerable<ReadingSentence> From(string text, int position)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, text.Length);
        return Enumerate(text, position);
    }

    private static IEnumerable<ReadingSentence> Enumerate(string text, int position)
    {
        var lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        while (true)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = text.Length;
            var line = text[lineStart..lineEnd];
            foreach (var span in Split(line))
            {
                if (lineStart + span.End <= position) continue;
                yield return new ReadingSentence(lineStart + span.Start, lineStart + span.End, line[span.Start..span.End]);
            }
            if (lineEnd == text.Length) yield break;
            lineStart = lineEnd + 1;
        }
    }

    private static bool IsTerminator(char c) => c is '.' or '!' or '?' or '…';

    private static bool IsCloser(char c) => c is '"' or '\'' or '”' or '’' or '»' or ')' or ']';

    /// <summary>Whether the word before the period at <paramref name="period"/> is an abbreviation ("nr", "ș.a").</summary>
    private static bool IsAbbreviation(string text, int period)
    {
        var start = period;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] == '.')) start--;
        if (start == period) return false;
        var word = text[start..period].ToLowerInvariant().Replace('ş', 'ș').Replace('ţ', 'ț');
        return Abbreviations.Contains(word);
    }
}
