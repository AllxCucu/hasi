using System.Text;

namespace Hasi.Documents.Tests;

// Inside the namespace: the enum Hasi.Core.Documents.DocumentFormat must win over Open XML's root namespace DocumentFormat.
using Hasi.Core.Documents;

public sealed class TextDocumentTests : TempFolder
{
    private readonly DocumentFormats _formats = new();

    private IReadOnlyList<string> LoadLines(byte[] bytes)
    {
        var path = PathOf("text.txt");
        File.WriteAllBytes(path, bytes);
        var result = _formats.Load(path);
        Assert.Equal(DocumentProblem.None, result.Problem);
        Assert.NotNull(result.Document);
        Assert.Equal(DocumentFormat.Text, result.Document.Format);
        Assert.True(result.Document.MadeByHasi);
        Assert.All(result.Document.Paragraphs, p => Assert.Equal(0, p.HeadingLevel));
        return result.Document.Paragraphs.Select(p => p.Text).ToList();
    }

    [Fact]
    public void Romanian_letters_survive_a_save_and_a_load_cedillas_kept_as_written()
    {
        var path = PathOf("scrisoare.txt");
        DocumentParagraph[] written =
        [
            new("Bună ziua, dragă Ștefan!"),
            new("ăâîșț ĂÂÎȘȚ"),
            new("Scris vechi cu sedilă: ŞŢ şţ"),
        ];

        Assert.Equal(DocumentProblem.None, _formats.Save(path, written, DocumentFormat.Text));
        var loaded = _formats.Load(path);

        Assert.Equal(DocumentProblem.None, loaded.Problem);
        Assert.Equal(written, loaded.Document!.Paragraphs);
        Assert.Empty(TemporaryFiles());
    }

    [Fact]
    public void Saved_text_is_utf8_with_a_byte_order_mark_and_windows_line_ends()
    {
        var path = PathOf("a.txt");

        _formats.Save(path, [new("ș"), new(""), new("ț")], DocumentFormat.Text);

        byte[] expected = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("ș\r\n\r\nț")];
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    [Fact]
    public void Utf8_with_a_byte_order_mark_is_read()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Țară\r\nșir")];

        Assert.Equal(["Țară", "șir"], LoadLines(bytes));
    }

    [Fact]
    public void Utf8_without_a_byte_order_mark_is_read()
    {
        Assert.Equal(["Țară", "șir"], LoadLines(Encoding.UTF8.GetBytes("Țară\nșir")));
    }

    [Fact]
    public void Utf16_little_endian_with_a_byte_order_mark_is_read()
    {
        byte[] bytes = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("Mâine\r\nîncă")];

        Assert.Equal(["Mâine", "încă"], LoadLines(bytes));
    }

    [Fact]
    public void Utf16_big_endian_with_a_byte_order_mark_is_read()
    {
        byte[] bytes = [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("Mâine\r\nîncă")];

        Assert.Equal(["Mâine", "încă"], LoadLines(bytes));
    }

    [Fact]
    public void Windows_1250_bytes_are_recognised_when_the_file_is_not_utf8()
    {
        // "aş ţine o mână" in the old Romanian code page: ş = 0xBA, ţ = 0xFE, â = 0xE2, ă = 0xE3.
        byte[] bytes = [(byte)'a', 0xBA, (byte)' ', 0xFE, (byte)'i', (byte)'n', (byte)'e', (byte)' ', (byte)'o', (byte)' ',
            (byte)'m', 0xE2, (byte)'n', 0xE3];

        Assert.Equal(["aş ţine o mână"], LoadLines(bytes));
    }

    [Fact]
    public void Lines_split_on_crlf_lf_and_cr()
    {
        Assert.Equal(["unu", "doi", "trei", "patru"], LoadLines(Encoding.UTF8.GetBytes("unu\r\ndoi\ntrei\rpatru")));
    }

    [Fact]
    public void Empty_lines_in_the_middle_are_kept()
    {
        Assert.Equal(["unu", "", "", "doi"], LoadLines(Encoding.UTF8.GetBytes("unu\r\n\r\n\ndoi")));
    }

    [Fact]
    public void A_final_line_end_does_not_add_an_empty_paragraph()
    {
        Assert.Equal(["unu", "doi"], LoadLines(Encoding.UTF8.GetBytes("unu\r\ndoi\r\n")));
    }

    [Fact]
    public void An_empty_file_has_no_paragraphs()
    {
        Assert.Empty(LoadLines([]));
        Assert.Empty(LoadLines([0xEF, 0xBB, 0xBF]));
    }
}
