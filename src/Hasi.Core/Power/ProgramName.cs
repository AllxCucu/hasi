using System.Text.RegularExpressions;

namespace Hasi.Core.Power;

/// <summary>
/// Turns what a program file says about itself into a name worth saying aloud. File descriptions are often
/// file names ("Notepad.exe"), code identifiers ("WhatsApp.Root") or longer than the product ("Windows Terminal Host").
/// </summary>
public static partial class ProgramName
{
    /// <param name="description">The file description, for example "Microsoft Word".</param>
    /// <param name="product">The product name, for example "Microsoft Office".</param>
    /// <param name="processName">The process name, always present, for example "WINWORD".</param>
    public static string Choose(string? description, string? product, string processName)
    {
        var d = Usable(StripExe(Clean(description)));
        var p = Usable(Clean(product));
        if (p is not null && p.Contains("Operating System", StringComparison.OrdinalIgnoreCase)) p = null;
        // "Windows Terminal Host" from the product "Windows Terminal": the product is what the user knows.
        if (d is not null && p is not null && d.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase)) return p;
        return d ?? p ?? FromIdentifier(StripExe(Clean(description)) is { Length: > 0 } raw ? raw : Clean(processName));
    }

    private static string Clean(string? text) =>
        Spaces().Replace((text ?? "").Replace("®", "", StringComparison.Ordinal).Replace("™", "", StringComparison.Ordinal)
            .Replace("©", "", StringComparison.Ordinal), " ").Trim();

    private static string StripExe(string text) =>
        text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text;

    /// <summary>Null for empty text and for identifiers such as "WhatsApp.Root" (a dot, no space).</summary>
    private static string? Usable(string text) =>
        text.Length == 0 || (text.Contains('.', StringComparison.Ordinal) && !text.Contains(' ', StringComparison.Ordinal)) ? null : text;

    /// <summary>"WhatsApp.Root" → "WhatsApp".</summary>
    private static string FromIdentifier(string text)
    {
        var first = text.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? text : first;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
