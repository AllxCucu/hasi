using System.Globalization;
using Hasi.Core.Localization;

namespace Hasi.Core.Tests;

public class LocalizationTests
{
    private static readonly string ResourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json");

    [Theory]
    [InlineData(0, PluralCategory.Few)]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(19, PluralCategory.Few)]
    [InlineData(20, PluralCategory.Other)]
    [InlineData(100, PluralCategory.Other)]
    [InlineData(101, PluralCategory.Few)]
    [InlineData(119, PluralCategory.Few)]
    [InlineData(120, PluralCategory.Other)]
    public void Romanian_plural_categories(long n, PluralCategory expected)
    {
        Assert.Equal(expected, RomanianPlural.Categorize(n));
    }

    [Theory]
    [InlineData(1, "1 opțiune")]
    [InlineData(4, "4 opțiuni")]
    [InlineData(12, "12 opțiuni")]
    [InlineData(20, "20 de opțiuni")]
    [InlineData(101, "101 opțiuni")]
    public void Plural_text_from_resources(long n, string expected)
    {
        var l = JsonLocalizer.LoadRomanian(ResourcePath);

        Assert.Equal(expected, l.Plural("menu.count.options", n));
    }

    [Fact]
    public void Placeholders_are_filled()
    {
        var l = JsonLocalizer.Parse("""{ "k": "{a} și {b}" }""", CultureInfo.GetCultureInfo("ro-RO"), RomanianPlural.Categorize);

        Assert.Equal("1 și doi", l.Get("k", ("a", 1), ("b", "doi")));
    }

    [Fact]
    public void Inserted_values_are_not_treated_as_placeholders()
    {
        var l = JsonLocalizer.Parse("""{ "k": "{a} și {b}" }""", CultureInfo.GetCultureInfo("ro-RO"), RomanianPlural.Categorize);

        Assert.Equal("{b} și doi", l.Get("k", ("a", "{b}"), ("b", "doi")));
    }

    [Fact]
    public void Missing_keys_are_visible_and_recorded()
    {
        var l = JsonLocalizer.Parse("{}", CultureInfo.GetCultureInfo("ro-RO"), RomanianPlural.Categorize);

        Assert.Equal("[nu.exista]", l.Get("nu.exista"));
        Assert.Contains("nu.exista", l.MissingKeys);
    }

    [Fact]
    public void Resources_use_comma_below_diacritics_only()
    {
        var text = File.ReadAllText(ResourcePath);

        // ş ţ Ş Ţ (cedilla) are wrong for Romanian; ș ț Ș Ț (comma below) are right.
        Assert.DoesNotContain('ş', text);
        Assert.DoesNotContain('ţ', text);
        Assert.DoesNotContain('Ş', text);
        Assert.DoesNotContain('Ţ', text);
    }

    [Fact]
    public void Spoken_resources_spell_out_key_names()
    {
        // Only the texts (values) are spoken; resource keys such as "keyName.LeftCtrl" may abbreviate.
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ResourcePath),
            new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var values = document.RootElement.EnumerateObject()
            .SelectMany(p => p.Value.ValueKind == System.Text.Json.JsonValueKind.Object
                ? p.Value.EnumerateObject().Select(o => o.Value.GetString()!)
                : [p.Value.GetString()!]);

        Assert.All(values, v => Assert.DoesNotContain("Ctrl", v));
    }
}
