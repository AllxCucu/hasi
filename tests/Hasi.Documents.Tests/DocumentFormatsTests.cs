namespace Hasi.Documents.Tests;

// Inside the namespace: the enum Hasi.Core.Documents.DocumentFormat must win over Open XML's root namespace DocumentFormat.
using Hasi.Core.Documents;

public sealed class DocumentFormatsTests : TempFolder
{
    private readonly DocumentFormats _formats = new();

    [Theory]
    [InlineData("scrisoare.txt", DocumentFormat.Text)]
    [InlineData(@"C:\Documente\NOTE.TXT", DocumentFormat.Text)]
    [InlineData("scrisoare.docx", DocumentFormat.Word)]
    [InlineData("Scrisoare.DocX", DocumentFormat.Word)]
    [InlineData("manual.pdf", DocumentFormat.Pdf)]
    [InlineData(@"D:\Carti\ROMAN.PDF", DocumentFormat.Pdf)]
    [InlineData("roman.epub", DocumentFormat.Epub)]
    [InlineData("Roman.EPUB", DocumentFormat.Epub)]
    public void Format_comes_from_the_extension(string path, DocumentFormat expected)
    {
        Assert.Equal(expected, _formats.FormatOf(path));
    }

    [Theory]
    [InlineData("vechi.doc")]
    [InlineData("carte.mobi")]
    [InlineData("carte.epub.zip")]
    [InlineData("fara-extensie")]
    [InlineData("arhiva.docx.zip")]
    public void Other_extensions_have_no_format(string path)
    {
        Assert.Null(_formats.FormatOf(path));
    }

    [Fact]
    public void Extensions_have_the_dot()
    {
        Assert.Equal(".txt", _formats.Extension(DocumentFormat.Text));
        Assert.Equal(".docx", _formats.Extension(DocumentFormat.Word));
        Assert.Equal(".pdf", _formats.Extension(DocumentFormat.Pdf));
        Assert.Equal(".epub", _formats.Extension(DocumentFormat.Epub));
    }

    [Theory]
    [InlineData(DocumentFormat.Pdf, "a.pdf")]
    [InlineData(DocumentFormat.Epub, "a.epub")]
    public void Pdf_and_epub_are_never_written(DocumentFormat format, string name)
    {
        Assert.Equal(DocumentProblem.Unsupported, _formats.Save(PathOf(name), [new("text")], format));
        Assert.Empty(Directory.GetFileSystemEntries(Folder));
    }

    [Theory]
    [InlineData("lipsa.txt")]
    [InlineData("lipsa.docx")]
    [InlineData("lipsa.pdf")]
    [InlineData("lipsa.epub")]
    public void A_missing_file_is_not_found(string name)
    {
        var result = _formats.Load(PathOf(name));

        Assert.Equal(DocumentProblem.NotFound, result.Problem);
        Assert.Null(result.Document);
    }

    [Fact]
    public void A_file_in_a_missing_folder_is_not_found()
    {
        Assert.Equal(DocumentProblem.NotFound, _formats.Load(Path.Combine(Folder, "nu-exista", "a.txt")).Problem);
    }

    [Fact]
    public void A_file_of_an_unknown_kind_is_unsupported()
    {
        var path = PathOf("vechi.doc");
        File.WriteAllText(path, "text");

        Assert.Equal(DocumentProblem.Unsupported, _formats.Load(path).Problem);
    }

    [Theory]
    [InlineData("mare.txt")]
    [InlineData("mare.docx")]
    public void A_file_larger_than_10_megabytes_is_too_large(string name)
    {
        var path = PathOf(name);
        using (var stream = File.Create(path)) stream.SetLength(11L * 1024 * 1024);

        var result = _formats.Load(path);

        Assert.Equal(DocumentProblem.TooLarge, result.Problem);
        Assert.Null(result.Document);
    }

    [Theory]
    [InlineData("carte.pdf")]
    [InlineData("carte.epub")]
    public void A_book_larger_than_50_megabytes_is_too_large(string name)
    {
        var path = PathOf(name);
        using (var stream = File.Create(path)) stream.SetLength(DocumentFormats.MaxBookSize + 1);

        Assert.Equal(DocumentProblem.TooLarge, _formats.Load(path).Problem);
    }

    [Theory]
    [InlineData("carte.pdf")]
    [InlineData("carte.epub")]
    public void A_book_of_11_megabytes_is_not_too_large(string name)
    {
        var path = PathOf(name);
        using (var stream = File.Create(path)) stream.SetLength(11L * 1024 * 1024);

        // Only zeros: opened, then found damaged.
        Assert.Equal(DocumentProblem.Damaged, _formats.Load(path).Problem);
    }

    [Fact]
    public void A_file_of_exactly_10_megabytes_is_opened()
    {
        var path = PathOf("limita.txt");
        File.WriteAllBytes(path, Enumerable.Repeat((byte)'a', (int)DocumentFormats.MaxFileSize).ToArray());

        Assert.Equal(DocumentProblem.None, _formats.Load(path).Problem);
    }

    [Theory]
    [InlineData(DocumentFormat.Text, "a.txt")]
    [InlineData(DocumentFormat.Word, "a.docx")]
    public void Saving_into_a_missing_folder_is_not_found_and_leaves_nothing_behind(DocumentFormat format, string name)
    {
        var missing = Path.Combine(Folder, "nu-exista");

        var problem = _formats.Save(Path.Combine(missing, name), [new("text")], format);

        Assert.Equal(DocumentProblem.NotFound, problem);
        Assert.False(Directory.Exists(missing));
        Assert.Empty(Directory.GetFileSystemEntries(Folder));
    }

    [Theory]
    [InlineData(DocumentFormat.Text, "a.txt")]
    [InlineData(DocumentFormat.Word, "a.docx")]
    public void A_failed_save_keeps_the_original_file_intact(DocumentFormat format, string name)
    {
        var path = PathOf(name);
        _formats.Save(path, [new("Originalul")], format);
        var original = File.ReadAllBytes(path);

        DocumentProblem problem;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // The file is locked by another program: the replace fails.
            problem = _formats.Save(path, [new("Versiunea nouă")], format);
        }

        Assert.Equal(DocumentProblem.NoAccess, problem);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(TemporaryFiles());
        Assert.Equal([new DocumentParagraph("Originalul")], _formats.Load(path).Document!.Paragraphs);
    }

    [Theory]
    [InlineData(DocumentFormat.Text, "a.txt")]
    [InlineData(DocumentFormat.Word, "a.docx")]
    public void Saving_over_an_existing_file_replaces_it(DocumentFormat format, string name)
    {
        var path = PathOf(name);
        _formats.Save(path, [new("Prima versiune")], format);

        Assert.Equal(DocumentProblem.None, _formats.Save(path, [new("A doua versiune")], format));

        Assert.Equal([new DocumentParagraph("A doua versiune")], _formats.Load(path).Document!.Paragraphs);
        Assert.Single(Directory.GetFiles(Folder));
    }

    [Fact]
    public void A_read_only_target_is_no_access_and_kept()
    {
        var path = PathOf("protejat.txt");
        _formats.Save(path, [new("Originalul")], DocumentFormat.Text);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.Equal(DocumentProblem.NoAccess, _formats.Save(path, [new("Nou")], DocumentFormat.Text));
            Assert.Equal([new DocumentParagraph("Originalul")], _formats.Load(path).Document!.Paragraphs);
            Assert.Empty(TemporaryFiles());
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void An_empty_document_can_be_saved_and_loaded()
    {
        var text = PathOf("gol.txt");
        var word = PathOf("gol.docx");

        Assert.Equal(DocumentProblem.None, _formats.Save(text, [], DocumentFormat.Text));
        Assert.Equal(DocumentProblem.None, _formats.Save(word, [], DocumentFormat.Word));

        Assert.Empty(_formats.Load(text).Document!.Paragraphs);
        Assert.Empty(_formats.Load(word).Document!.Paragraphs);
    }
}
