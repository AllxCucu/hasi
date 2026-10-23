namespace Hasi.Text.Ro.Tests;

public class AcronymLexiconTests
{
    [Fact]
    public void Parses_letters_word_and_explicit_pronunciations()
    {
        var lexicon = AcronymLexicon.Parse(["# comentariu", "", "BNR => litere", "OMS => cuvânt", "RATB => ra te be"]);

        Assert.Equal(3, lexicon.Count);
        Assert.Equal("be ne re la Oms și ra te be", lexicon.Expand("BNR la OMS și RATB"));
    }

    [Fact]
    public void Malformed_lines_are_ignored()
    {
        var lexicon = AcronymLexicon.Parse(["fara separator", "=> nimic", "X =>"]);

        Assert.Equal(0, lexicon.Count);
    }

    [Fact]
    public void Missing_file_means_no_acronyms()
    {
        var lexicon = AcronymLexicon.Load(Path.Combine(AppContext.BaseDirectory, "nu-exista.txt"));

        Assert.Equal(0, lexicon.Count);
    }

    [Fact]
    public void The_shipped_file_contains_the_testers_list()
    {
        var lexicon = AcronymLexicon.Load(AcronymLexicon.DefaultPath);

        Assert.Equal(13, lexicon.Count);
    }

    [Theory]
    [InlineData("CFR", "ce fe re")]
    [InlineData("ȘȚ", "șe țe")]
    [InlineData("WXY", "dublu ve ics i grec")]
    public void Romanian_letter_names(string acronym, string spoken)
    {
        Assert.Equal(spoken, AcronymLexicon.SpellLetters(acronym));
    }
}
