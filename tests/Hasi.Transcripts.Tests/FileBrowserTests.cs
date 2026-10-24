using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

public class FileBrowserTests
{
    private const long MB = 1024L * 1024;
    private const long GB = 1024L * MB;

    private const string Documents = @"C:\Users\Ana\Documents";
    private const string Desktop = @"C:\Users\Ana\Desktop";
    private const string Downloads = @"C:\Users\Ana\Downloads";

    private static void AddKnownFolders(FakeFileSystem files) => files
        .AddPlace(PlaceKind.Documents, Documents)
        .AddPlace(PlaceKind.Desktop, Desktop)
        .AddPlace(PlaceKind.Downloads, Downloads);

    /// <summary>Documents with folders, documents of both types in Romanian order traps, and files of other types.</summary>
    private static void AddDocuments(FakeFileSystem files) => files
        .AddFile($@"{Documents}\Țară.txt", 120)
        .AddFile($@"{Documents}\Tabel.txt", 80)
        .AddFile($@"{Documents}\Ședință.docx", 2048)
        .AddFile($@"{Documents}\Sumar.txt", 10)
        .AddFile($@"{Documents}\Scrisoare.docx", 4096)
        .AddFile($@"{Documents}\ana.txt", 5)
        .AddFile($@"{Documents}\Buget.xlsx", 9000)
        .AddFile($@"{Documents}\poza.jpg", 70000)
        .AddFile($@"{Documents}\Scrisori\Mama.TXT", 300)
        .AddFile($@"{Documents}\Rețete\tort.jpg", 50000);

    /// <summary>An "Editor" menu whose only item opens the browser; the chosen file is said back and kept.</summary>
    private static (ShellHarness Harness, List<FileEntry> Chosen) Opened(Action<FakeFileSystem> setup)
    {
        var h = new ShellHarness();
        setup(h.Files);
        List<FileEntry> chosen = [];
        var browser = new MenuItem("Deschide un document", _ => ScreenResult.Push(new FileBrowserScreen(
            "Deschide un document", h.Files, [".txt", "DOCX"],
            (entry, _) =>
            {
                chosen.Add(entry);
                return ScreenResult.Say($"Ales: {entry.Path}");
            })));
        h.Start(new MenuScreen("Editor", [browser]));
        h.Press(Key.Enter);
        return (h, chosen);
    }

    [Fact]
    public void First_level_lists_the_known_folders()
    {
        var (h, _) = Opened(AddKnownFolders);
        Assert.Equal("Deschide un document. 3 opțiuni. Selectat: Documente, 1 din 3.", h.LastSaid);
        Assert.Equal(["Desktop, 2 din 3.", "Descărcări, 3 din 3.", "Sfârșitul listei. Descărcări, 3 din 3."],
            h.Press(Key.Down, Key.Down, Key.Down));
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
    }

    [Theory]
    [InlineData("Kingston", 15.7, "Stick USB, Kingston, 15 gigaocteți liberi")]
    [InlineData(null, 1.0, "Stick USB, 1 gigaoctet liber")]
    [InlineData("", 2.5, "Stick USB, 2 gigaocteți liberi")]
    [InlineData("SANDISK", 28.9, "Stick USB, SANDISK, 28 de gigaocteți liberi")]
    [InlineData("Poze", 800.0 / 1024, "Stick USB, Poze, 800 de megaocteți liberi")]
    [InlineData("Mic", 1.0 / 1024, "Stick USB, Mic, 1 megaoctet liber")]
    [InlineData("Plin", 0.0, "Stick USB, Plin, 0 megaocteți liberi")]
    public void A_stick_is_said_with_its_label_and_free_space(string? label, double freeGigabytes, string expected)
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            files.AddPlace(PlaceKind.UsbStick, @"E:\", label, (long)(freeGigabytes * GB));
        });
        Assert.Equal("Deschide un document. 4 opțiuni. Selectat: Documente, 1 din 4.", h.LastSaid);
        Assert.Equal([$"{expected}, 4 din 4."], h.Press(Key.End));
    }

    [Fact]
    public void A_stick_with_unknown_free_space_is_said_without_it()
    {
        var (h, _) = Opened(files => files.AddPlace(PlaceKind.UsbStick, @"E:\", "Kingston"));
        Assert.Equal("Deschide un document. 1 opțiune. Selectat: Stick USB, Kingston, 1 din 1.", h.LastSaid);
    }

    [Fact]
    public void A_folder_shows_folders_first_then_only_text_and_word_documents_in_romanian_order()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            AddDocuments(files);
        });
        Assert.Equal(["Documente. 8 opțiuni. Selectat: Dosarul Rețete, 1 din 8."], h.Press(Key.Enter));
        Assert.Equal(
        [
            "Dosarul Scrisori, 2 din 8.",
            "ana, text, 3 din 8.",
            "Scrisoare, document Word, 4 din 8.",
            "Sumar, text, 5 din 8.",
            "Ședință, document Word, 6 din 8.",
            "Tabel, text, 7 din 8.",
            "Țară, text, 8 din 8.",
        ], h.Press(Key.Down, Key.Down, Key.Down, Key.Down, Key.Down, Key.Down, Key.Down));
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
    }

    [Fact]
    public void Enter_goes_into_a_folder_and_escape_goes_back_one_level_keeping_the_selection()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            AddDocuments(files);
        });
        h.Press(Key.Enter, Key.Down);
        Assert.Equal(["Scrisori. 1 opțiune. Selectat: Mama, text, 1 din 1."], h.Press(Key.Enter));
        Assert.Equal(["Sunteți în: Editor, Deschide un document, Documente, Scrisori."],
            h.Press(new KeyChord(Key.U, KeyModifiers.Ctrl)));
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
        Assert.Equal(["Documente. Selectat: Dosarul Scrisori, 2 din 8."], h.Press(Key.Escape));
        Assert.Equal(["Deschide un document. Selectat: Documente, 1 din 3."], h.Press(Key.Escape));
        Assert.Equal(["Editor. Selectat: Deschide un document, 1 din 1."], h.Press(Key.Escape));
    }

    [Fact]
    public void A_stick_opens_with_its_short_name_as_title()
    {
        var (h, _) = Opened(files => files
            .AddPlace(PlaceKind.UsbStick, @"E:\", "Kingston", 15 * GB)
            .AddFile(@"E:\Cerere.docx"));
        Assert.Equal(["Stick USB, Kingston. 1 opțiune. Selectat: Cerere, document Word, 1 din 1."], h.Press(Key.Enter));
    }

    [Fact]
    public void An_empty_folder_says_there_are_no_documents()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            AddDocuments(files);
        });
        Assert.Equal(["Rețete. Nu există documente aici. Apăsați Escape pentru a vă întoarce."], h.Press(Key.Enter, Key.Enter).Skip(1));
        Assert.Equal(["Rețete. Nu există documente aici. Apăsați Escape pentru a vă întoarce."], h.Press(Key.Down));
        Assert.StartsWith("Ajutor pentru Rețete. Aici nu este niciun document", h.Press(Key.F1)[0], StringComparison.Ordinal);
        Assert.Equal(["Documente. Selectat: Dosarul Rețete, 1 din 8."], h.Press(Key.Escape));
    }

    [Fact]
    public void A_folder_that_cannot_be_read_behaves_as_empty()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            files.AddFile($@"{Desktop}\Notă.txt");
            files.Unreadable.Add(Desktop);
        });
        Assert.Equal(["Desktop. Nu există documente aici. Apăsați Escape pentru a vă întoarce."], h.Press(Key.Down, Key.Enter).Skip(1));
    }

    [Fact]
    public void No_places_at_all_is_said_plainly()
    {
        var (h, _) = Opened(_ => { });
        Assert.Equal("Deschide un document. Nu am găsit niciun loc pentru documente. Apăsați Escape pentru a vă întoarce.", h.LastSaid);
        Assert.Equal(["Editor. Selectat: Deschide un document, 1 din 1."], h.Press(Key.Escape));
    }

    [Fact]
    public void Enter_on_a_file_calls_the_callback_with_that_file()
    {
        var (h, chosen) = Opened(files =>
        {
            AddKnownFolders(files);
            AddDocuments(files);
        });
        h.Press(Key.Enter, Key.Down, Key.Enter);
        Assert.Equal([@"Ales: C:\Users\Ana\Documents\Scrisori\Mama.TXT"], h.Press(Key.Enter));
        var entry = Assert.Single(chosen);
        Assert.Equal("Mama.TXT", entry.Name);
        Assert.Equal(300, entry.Size);
        Assert.False(entry.IsFolder);
    }

    [Fact]
    public void Places_are_read_again_each_time_the_browser_opens()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            files.AddPlace(PlaceKind.UsbStick, @"E:\", "Kingston", 15 * GB);
        });
        Assert.Equal("Deschide un document. 4 opțiuni. Selectat: Documente, 1 din 4.", h.LastSaid);
        h.Press(Key.Escape);
        h.Files.RemovePlace(@"E:\");
        Assert.Equal(["Deschide un document. 3 opțiuni. Selectat: Documente, 1 din 3."], h.Press(Key.Enter));
    }

    [Fact]
    public void Help_explains_places_and_folders()
    {
        var (h, _) = Opened(files =>
        {
            AddKnownFolders(files);
            AddDocuments(files);
        });
        var places = h.Press(Key.F1)[0];
        Assert.StartsWith("Ajutor pentru Deschide un document.", places, StringComparison.Ordinal);
        Assert.Contains("Alegeți locul în care este documentul", places, StringComparison.Ordinal);
        h.Press(Key.Enter);
        Assert.Contains("Mai întâi sunt dosarele, apoi documentele.", h.Press(Key.F1)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Place_picker_lists_only_places_and_enter_chooses_one()
    {
        var h = new ShellHarness();
        AddKnownFolders(h.Files);
        AddDocuments(h.Files);
        h.Files.AddPlace(PlaceKind.UsbStick, @"E:\", "Kingston", 15 * GB);
        List<Place> chosen = [];
        h.Start(new MenuScreen("Editor", [new MenuItem("Salvează o copie", _ => ScreenResult.Push(new PlacePickerScreen(
            "Unde salvez copia", h.Files, (place, _) =>
            {
                chosen.Add(place);
                return ScreenResult.Pop(prefix: $"Ales: {place.Path}", announce: false);
            })))]));

        Assert.Equal(["Unde salvez copia. 4 opțiuni. Selectat: Documente, 1 din 4."], h.Press(Key.Enter));
        Assert.Equal(["Stick USB, Kingston, 15 gigaocteți liberi, 4 din 4."], h.Press(Key.End));
        Assert.Contains("Alegeți locul în care se pune documentul", h.Press(Key.F1)[0], StringComparison.Ordinal);
        h.Press(Key.Enter);
        Assert.Equal(PlaceKind.UsbStick, Assert.Single(chosen).Kind);
        Assert.Equal("Editor", h.Shell.Current.Title);
    }
}
