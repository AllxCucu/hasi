using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hasi.Core.Localization;

/// <summary>Looks up user-facing text. All UI text lives in the resource file, never in code.</summary>
public interface ILocalizer
{
    CultureInfo Culture { get; }

    /// <summary>Text for <paramref name="key"/> with <c>{name}</c> placeholders filled in.</summary>
    string Get(string key, params (string Name, object Value)[] args);

    /// <summary>Plural-aware text; <c>{count}</c> is filled with <paramref name="count"/>.</summary>
    string Plural(string key, long count, params (string Name, object Value)[] args);
}

/// <summary>
/// Loads a flat JSON resource file. A value is either a string or a plural object with the CLDR
/// categories of the language, for Romanian <c>{"one": …, "few": …, "other": …}</c>.
/// </summary>
public sealed class JsonLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<PluralCategory, string>> _plurals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private readonly Func<long, PluralCategory> _pluralRule;

    private JsonLocalizer(CultureInfo culture, Func<long, PluralCategory> pluralRule)
    {
        Culture = culture;
        _pluralRule = pluralRule;
    }

    public CultureInfo Culture { get; }

    /// <summary>Keys that were requested but are not in the resource file.</summary>
    public IReadOnlyCollection<string> MissingKeys => _missing;

    public IEnumerable<string> Keys => _texts.Keys.Concat(_plurals.Keys);

    public static JsonLocalizer LoadRomanian(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), CultureInfo.GetCultureInfo("ro-RO"), RomanianPlural.Categorize);

    public static JsonLocalizer Parse(string json, CultureInfo culture, Func<long, PluralCategory> pluralRule)
    {
        var localizer = new JsonLocalizer(culture, pluralRule);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        foreach (var property in document.RootElement.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    localizer._texts[property.Name] = property.Value.GetString()!;
                    break;
                case JsonValueKind.Object:
                    localizer._plurals[property.Name] = property.Value.EnumerateObject().ToDictionary(
                        p => Enum.Parse<PluralCategory>(p.Name, ignoreCase: true),
                        p => p.Value.GetString()!);
                    break;
                default:
                    throw new FormatException($"Resource '{property.Name}' must be a string or a plural object.");
            }
        }
        return localizer;
    }

    public string Get(string key, params (string Name, object Value)[] args)
    {
        if (!_texts.TryGetValue(key, out var template))
        {
            _missing.Add(key);
            return $"[{key}]";
        }
        return Fill(template, args);
    }

    public string Plural(string key, long count, params (string Name, object Value)[] args)
    {
        if (!_plurals.TryGetValue(key, out var forms))
        {
            _missing.Add(key);
            return $"[{key}]";
        }
        var category = _pluralRule(count);
        if (!forms.TryGetValue(category, out var template) && !forms.TryGetValue(PluralCategory.Other, out template))
        {
            _missing.Add($"{key}.{category}");
            return $"[{key}]";
        }
        return Fill(template, [("count", count), .. args]);
    }

    /// <summary>Fills placeholders in one pass, so inserted values are never read as placeholders.</summary>
    private string Fill(string template, (string Name, object Value)[] args)
    {
        if (args.Length == 0) return template;
        return Placeholder.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            foreach (var arg in args)
            {
                if (arg.Name == name) return Convert.ToString(arg.Value, Culture) ?? "";
            }
            return match.Value;
        });
    }

    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}

public enum PluralCategory
{
    Zero,
    One,
    Two,
    Few,
    Many,
    Other,
}

/// <summary>CLDR plural rules for Romanian (integers): 1 opțiune, 2 opțiuni, 20 de opțiuni.</summary>
public static class RomanianPlural
{
    public static PluralCategory Categorize(long n)
    {
        n = Math.Abs(n);
        if (n == 1) return PluralCategory.One;
        var lastTwo = n % 100;
        return n == 0 || lastTwo is >= 1 and <= 19 ? PluralCategory.Few : PluralCategory.Other;
    }
}
