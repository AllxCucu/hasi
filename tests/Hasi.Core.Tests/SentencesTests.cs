using Hasi.Core.Text;

namespace Hasi.Core.Tests;

/// <summary>Splitting text into sentences for "citește tot". Sentences in a row are separated by " | ".</summary>
public class SentencesTests
{
    [Theory]
    [InlineData("Bună ziua. Ce mai faci?", "Bună ziua. | Ce mai faci?")]
    [InlineData("Vino! Acum!", "Vino! | Acum!")]
    [InlineData("Ce? Nu știu.", "Ce? | Nu știu.")]
    [InlineData("Așa e… Poate.", "Așa e… | Poate.")]
    [InlineData("Așa e... Poate.", "Așa e... | Poate.")]
    [InlineData("Cum?! Așa.", "Cum?! | Așa.")]
    [InlineData("Fără punct la sfârșit", "Fără punct la sfârșit")]
    [InlineData("  Spații în jur.   ", "Spații în jur.")]
    [InlineData("A spus „Vin.” Apoi a plecat.", "A spus „Vin.” | Apoi a plecat.")]
    [InlineData("(Așa a fost.) Gata.", "(Așa a fost.) | Gata.")]
    [InlineData("Costă 3.50 lei. Bine.", "Costă 3.50 lei. | Bine.")]
    [InlineData("www.hasi.ro e site-ul. Da.", "www.hasi.ro e site-ul. | Da.")]
    [InlineData("Locuiesc pe str. Florilor nr. 5. Vino.", "Locuiesc pe str. Florilor nr. 5. | Vino.")]
    [InlineData("Dl. Popescu și dna. Ionescu au venit.", "Dl. Popescu și dna. Ionescu au venit.")]
    [InlineData("Mere, pere ș.a. Restul mâine.", "Mere, pere ș.a. Restul mâine.")]
    [InlineData("Mere, pere ş.a. Restul mâine.", "Mere, pere ş.a. Restul mâine.")]
    [InlineData("Vezi art. 3 alin. 2 lit. b pct. 4.", "Vezi art. 3 alin. 2 lit. b pct. 4.")]
    [InlineData("Sună la tel. 0722. Mersi.", "Sună la tel. 0722. | Mersi.")]
    [InlineData("Prof. dr. Ionescu, din jud. Cluj.", "Prof. dr. Ionescu, din jud. Cluj.")]
    [InlineData("Biserica Sf. Nicolae, bl. 4, sc. A, ap. 12.", "Biserica Sf. Nicolae, bl. 4, sc. A, ap. 12.")]
    [InlineData("Mere, pere etc. Și altele.", "Mere, pere etc. Și altele.")]
    [InlineData("Am cumpărat un disc. Bun.", "Am cumpărat un disc. | Bun.")]
    [InlineData("Se termină cu nr.", "Se termină cu nr.")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Splits_a_paragraph_into_sentences(string paragraph, string expected)
    {
        var sentences = Sentences.Split(paragraph).Select(s => paragraph[s.Start..s.End]);

        Assert.Equal(expected, string.Join(" | ", sentences));
    }

    [Fact]
    public void The_end_of_a_paragraph_always_ends_a_sentence()
    {
        var sentences = Sentences.From("Titlu\nPrimul rând fără punct\nnr.\nGata.", 0).Select(s => s.Text);

        Assert.Equal(["Titlu", "Primul rând fără punct", "nr.", "Gata."], sentences);
    }

    [Theory]
    [InlineData(0, "Unu. | Doi. | Trei.")] // at the start
    [InlineData(2, "Unu. | Doi. | Trei.")] // inside the first sentence
    [InlineData(3, "Unu. | Doi. | Trei.")] // on its period
    [InlineData(4, "Doi. | Trei.")] // on the space after it: the next one
    [InlineData(7, "Doi. | Trei.")]
    [InlineData(9, "Trei.")] // at the end of the line
    [InlineData(10, "Trei.")]
    [InlineData(15, "")] // at the end of the text
    public void Reading_starts_with_the_sentence_that_contains_the_position(int position, string expected)
    {
        const string text = "Unu. Doi.\nTrei.";

        var sentences = Sentences.From(text, position).Select(s => s.Text);

        Assert.Equal(expected, string.Join(" | ", sentences));
    }

    [Fact]
    public void Each_sentence_knows_where_it_is_in_the_whole_text()
    {
        const string text = "Unu. Doi.\n\n  Trei.";

        var sentences = Sentences.From(text, 0).ToList();

        Assert.Equal([(0, 4), (5, 9), (13, 18)], sentences.Select(s => (s.Start, s.End)));
        Assert.All(sentences, s => Assert.Equal(s.Text, text[s.Start..s.End]));
    }
}
