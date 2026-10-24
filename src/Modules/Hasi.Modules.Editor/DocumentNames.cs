using Hasi.Core.Text;

namespace Hasi.Modules.Editor;

/// <summary>Names for saved documents: the proposal from the text, and cleaning what the user typed.</summary>
internal static class DocumentNames
{
    public const int ProposalWords = 5;
    public const int MaxProposalLength = 40;

    /// <summary>Characters Windows does not allow in file names (control characters are refused too).</summary>
    private const string InvalidCharacters = "<>:\"/\\|?*";

    public static bool IsInvalid(char c) => char.IsControl(c) || InvalidCharacters.Contains(c, StringComparison.Ordinal);

    /// <summary>
    /// The first <see cref="ProposalWords"/> words of the first line with text, without the characters Windows refuses,
    /// at most <see cref="MaxProposalLength"/> characters (cut between words when possible), without punctuation at the
    /// end; <paramref name="fallback"/> when nothing is left.
    /// </summary>
    public static string Proposal(string text, string fallback) => FirstWords(text, fallback, MaxProposalLength);

    /// <summary>
    /// How a draft is named when it is offered: the first words as in <see cref="Proposal"/>, without the length cut;
    /// <paramref name="fallback"/> (the document's title) when the text has none.
    /// </summary>
    public static string Words(string text, string fallback) => FirstWords(text, fallback, maxLength: null);

    private static string FirstWords(string text, string fallback, int? maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var line = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        var words = line
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string([.. word.Where(c => !IsInvalid(c))]))
            .Where(word => word.Length > 0)
            .Take(ProposalWords);
        var name = string.Join(' ', words);
        if (maxLength is { } max && name.Length > max)
        {
            var cut = name.LastIndexOf(' ', max);
            name = cut > 0 ? name[..cut] : name[..max];
        }
        // "Dragă Ana," is proposed as "Dragă Ana": punctuation at the end of a name only gets in the way.
        var end = name.Length;
        while (end > 0 && !TextBuffer.IsWordCharacter(name[end - 1])) end--;
        name = name[..end];
        return name.Length == 0 ? fallback : name;
    }

    /// <summary>
    /// What the user typed, made a valid file name: the characters Windows refuses become spaces, the extension
    /// (if typed) and the spaces and dots at the ends are removed. Null when nothing is left.
    /// </summary>
    public static string? Clean(string typed, string extension)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var name = new string([.. typed.Select(c => IsInvalid(c) ? ' ' : c)]).Trim();
        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) name = name[..^extension.Length];
        name = name.Trim().TrimEnd('.', ' ');
        return name.Length == 0 ? null : name;
    }
}
