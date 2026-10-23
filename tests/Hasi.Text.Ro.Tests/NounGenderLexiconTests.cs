namespace Hasi.Text.Ro.Tests;

public class NounGenderLexiconTests
{
    [Fact]
    public void The_shipped_dictionary_is_loaded()
    {
        var lexicon = NounGenderLexicon.LoadDefault();

        Assert.True(lexicon.DictionaryCount > 100_000, $"only {lexicon.DictionaryCount} forms");
    }

    [Fact]
    public void The_testers_file_wins_over_the_dictionary_and_the_built_in_list()
    {
        var lexicon = NounGenderLexicon.FromTables(user: ["ore => m", "autobuze => n"], dictionary: ["autobuze => f"]);

        Assert.Equal(Gender.Masculine, lexicon.Find("ore").Gender);
        Assert.Equal(Gender.Neuter, lexicon.Find("autobuze").Gender);
    }

    [Fact]
    public void Function_words_and_months_are_never_counted_nouns()
    {
        var lexicon = NounGenderLexicon.FromTables(user: [], dictionary: ["mai => m"]);

        Assert.False(lexicon.Find("din").IsNoun);
        Assert.False(lexicon.Find("mai").IsNoun);
        Assert.False(lexicon.Find("este").IsNoun);
    }

    [Fact]
    public void Unknown_lower_case_words_get_a_guessed_gender_from_their_ending()
    {
        var lexicon = NounGenderLexicon.BuiltIn;

        Assert.Equal(new NounLookup(true, Gender.Feminine, Guessed: true), lexicon.Find("căsuță"));
        Assert.Equal(new NounLookup(true, Gender.Feminine, Guessed: true), lexicon.Find("frezie"));
        Assert.Equal(new NounLookup(true, Gender.Masculine, Guessed: true), lexicon.Find("xyzw"));
        Assert.False(lexicon.Find("Cluj").IsNoun);
    }

    [Fact]
    public void Unknown_plurals_in_e_take_the_feminine_form_and_plurals_in_i_stay_guessed()
    {
        var lexicon = NounGenderLexicon.BuiltIn;

        Assert.Equal(new NounLookup(true, Gender.Neuter, Guessed: false), lexicon.Find("smartfoane", plural: true));
        Assert.Equal(new NounLookup(true, Gender.Masculine, Guessed: true), lexicon.Find("bitcoini", plural: true));
    }

    [Fact]
    public void Label_nouns_are_recognized_even_without_the_dictionary()
    {
        Assert.True(NounGenderLexicon.BuiltIn.IsKnownNoun("etajul"));
        Assert.False(NounGenderLexicon.BuiltIn.IsKnownNoun("din"));
    }

    [Fact]
    public void A_guessed_plural_in_i_is_reported()
    {
        var reported = new List<string>();
        var normalizer = new RomanianNormalizer(AcronymLexicon.Empty, NounGenderLexicon.BuiltIn, reported.Add);

        normalizer.Normalize("2 bitcoini și 2 smartfoane");

        Assert.Equal(["bitcoini"], reported);
    }

    [Fact]
    public void Guessed_genders_are_reported_only_where_they_change_the_words()
    {
        var reported = new List<string>();
        var normalizer = new RomanianNormalizer(AcronymLexicon.Empty, NounGenderLexicon.BuiltIn, reported.Add);

        normalizer.Normalize("2 xyzw, 5 abcd, 21 qwer, 11 zzzz");

        Assert.Equal(["xyzw", "qwer"], reported);
    }

    [Fact]
    public void Malformed_lines_are_ignored()
    {
        var table = NounGenderLexicon.Parse(["# comentariu", "fara", "x => q", "=> m", "bun => m"]);

        Assert.Equal(["bun"], table.Keys);
    }
}
