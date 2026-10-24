using Hasi.Core.Diagnostics;
using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Modules.Documents;
using Hasi.Modules.Exit;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

// Inside the namespace: the enum must win over Open XML's root namespace DocumentFormat (Hasi.Documents is referenced).
using Hasi.Core.Documents;

/// <summary>Module 3, reading documents, with the real Romanian texts, a fake disk and documents in memory.</summary>
public class DocumentsModuleTests
{
    private const string Documents = @"C:\Users\Ana\Documents";
    private const string Opening = "Se deschide documentul…";
    private const string RootReturn = "Citire documente. Selectat: {0}, {1} din 2.";
    private const string RecentName = "documente-recente";

    private static readonly KeyChord CtrlRight = new(Key.Right, KeyModifiers.Ctrl);

    /// <summary>The recent list as the module stores it.</summary>
    private sealed record StoredReading(
        string Path, string Title, DocumentFormat Format, ReadingPosition Position, int Page, DateTimeOffset LastRead);

    private sealed record Setup(ShellHarness H, FakeDocumentFormats Docs, ManualEventLoop? Loop, ManualTimeProvider Time)
    {
        public IReadOnlyList<StoredReading> Recent => H.UserData.Read<List<StoredReading>>(RecentName) ?? [];

        public DocumentReaderScreen Reader => Assert.IsType<DocumentReaderScreen>(H.Shell.Current);
    }

    private static string InDocuments(string name) => $@"{Documents}\{name}";

    /// <summary>HASI with the module and Ieșire, on the module's menu; Documente on the disk.</summary>
    /// <param name="manualLoop">Background loading waits for the test (<see cref="ManualEventLoop"/>); otherwise it runs at once.</param>
    private static Setup OnDocumentsMenu(Action<FakeDocumentFormats>? documents = null, bool manualLoop = false, IDiagnosticLog? log = null)
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 24, 11, 32, 0, TimeSpan.Zero),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        var loop = manualLoop ? new ManualEventLoop(time) : null;
        var docs = new FakeDocumentFormats();
        var h = new ShellHarness(time: time, diagnosticLog: log, documents: docs, eventLoop: loop);
        h.Files.AddPlace(PlaceKind.Documents, Documents);
        documents?.Invoke(docs);
        h.StartWithModules(new DocumentsModule(), new ExitModule());
        h.Press(Key.Enter);
        return new Setup(h, docs, loop, time);
    }

    /// <summary>"Deschide un document", then Documente: the folder's level of the browser.</summary>
    private static IReadOnlyList<string> ToDocumentsFolder(ShellHarness h) => h.Press(Key.Home, Key.Enter, Key.Enter);

    /// <summary>Opens the document at <paramref name="position"/> in Documente (Romanian order); returns what Enter said.</summary>
    private static IReadOnlyList<string> OpenFromDocuments(ShellHarness h, int position = 1)
    {
        ToDocumentsFolder(h);
        for (var i = 1; i < position; i++) h.Press(Key.Down);
        return h.Press(Key.Enter);
    }

    /// <summary>"Documente recente", then Enter on the item at <paramref name="position"/>; returns what that Enter said.</summary>
    private static IReadOnlyList<string> OpenFromRecents(ShellHarness h, int position = 1)
    {
        h.Press(Key.End, Key.Enter);
        for (var i = 1; i < position; i++) h.Press(Key.Down);
        return h.Press(Key.Enter);
    }

    /// <summary>A PDF of <paramref name="pages"/> pages, "Pagina 1." to "Pagina n.", with the given pages scanned.</summary>
    private static Document Pdf(int pages, params int[] scanned) =>
        FakeDocumentFormats.Pdf([.. Enumerable.Range(1, pages).Select(p => scanned.Contains(p) ? null : $"Pagina {p}.")]);

    private static Document Book() => FakeDocumentFormats.Epub(
        ("Unu", ["Era odată."]), ("Doi", ["Apoi a fost."]), ("Trei", ["Și gata."]));

    // --- The module's menu ---

    [Fact]
    public void The_menu_offers_opening_a_document_and_the_recent_ones()
    {
        var s = OnDocumentsMenu();

        Assert.Equal("Citire documente. 2 opțiuni. Selectat: Deschide un document, 1 din 2.", s.H.LastSaid);
        Assert.Equal(["Documente recente, 2 din 2."], s.H.Press(Key.Down));
    }

    [Fact]
    public void No_recent_documents_is_said_without_leaving_the_menu()
    {
        var s = OnDocumentsMenu();

        Assert.Equal(["Documente recente, 2 din 2.", "Nu aveți documente recente."], s.H.Press(Key.End, Key.Enter));
        Assert.IsType<MenuScreen>(s.H.Shell.Current);
    }

    [Fact]
    public void The_browser_shows_text_word_pdf_and_epub_documents_only()
    {
        var s = OnDocumentsMenu(docs => docs
            .Add(InDocuments("Scrisoare.txt"), "Dragă Ana,")
            .Add(InDocuments("Rețete.docx"), "Cozonac")
            .Add(InDocuments("Manual.pdf"), Pdf(2))
            .Add(InDocuments("Carte.epub"), Book()));
        s.H.Files.AddFile(InDocuments("Tabel.odt"));

        Assert.Equal("Documente. 4 opțiuni. Selectat: Carte, carte EPUB, 1 din 4.", ToDocumentsFolder(s.H)[^1]);
        Assert.Equal(["Manual, document PDF, 2 din 4.", "Rețete, document Word, 3 din 4.", "Scrisoare, text, 4 din 4."],
            s.H.Press(Key.Down, Key.Down, Key.Down));
    }

    // --- Opening each format ---

    [Theory]
    [InlineData("Scrisoare.txt", "Scrisoare. Dragă Ana,")]
    [InlineData("Rețete.docx", "Rețete. Dragă Ana,")]
    [InlineData("Manual.pdf", "Manual. Pagina 1.")]
    [InlineData("Carte.epub", "Carte. Titlu 1: Unu")]
    public void Each_format_opens_in_the_reader_above_the_module_menu(string name, string expected)
    {
        var path = InDocuments(name);
        var s = OnDocumentsMenu(docs =>
        {
            if (name.EndsWith(".pdf", StringComparison.Ordinal)) docs.Add(path, Pdf(3));
            else if (name.EndsWith(".epub", StringComparison.Ordinal)) docs.Add(path, Book());
            else docs.Add(path, "Dragă Ana,\nCe mai faci?");
        });

        Assert.Equal([expected], OpenFromDocuments(s.H));
        Assert.True(s.H.LastAnnouncement!.Utterance.Sensitive);
        Assert.Equal(KeyProfile.TextReading, s.Reader.KeyProfile);
        Assert.Equal(["Meniu principal", "Citire documente", Path.GetFileNameWithoutExtension(name)], s.H.Shell.Stack.Select(x => x.Title));
        var recent = Assert.Single(s.Recent);
        Assert.Equal((path, Path.GetFileNameWithoutExtension(name), ReadingPosition.Beginning), (recent.Path, recent.Title, recent.Position));

        Assert.Equal([string.Format(null, RootReturn, "Deschide un document", 1)], s.H.Press(Key.Escape));
    }

    // --- Loading in the background ---

    [Fact]
    public void Opening_runs_in_the_background_and_says_so_after_a_second()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,"), manualLoop: true);
        ToDocumentsFolder(s.H);

        Assert.Empty(s.H.Press(Key.Enter));
        Assert.IsType<FileBrowserScreen>(s.H.Shell.Current);
        var before = s.H.Output.Announcements.Count;
        s.Loop!.Advance(TimeSpan.FromSeconds(1));
        s.Loop.Finish();

        Assert.Equal([Opening, "Scrisoare. Dragă Ana,"], s.H.Output.Announcements.Skip(before).Select(a => a.Utterance.Text));
        Assert.IsType<DocumentReaderScreen>(s.H.Shell.Current);
    }

    [Fact]
    public void A_quick_opening_says_no_notice()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,"), manualLoop: true);
        ToDocumentsFolder(s.H);
        s.H.Press(Key.Enter);
        var before = s.H.Output.Announcements.Count;

        s.Loop!.Finish();
        s.Loop.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(["Scrisoare. Dragă Ana,"], s.H.Output.Announcements.Skip(before).Select(a => a.Utterance.Text));
    }

    [Fact]
    public void Enter_again_while_opening_says_it_is_opening_and_loads_once()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,"), manualLoop: true);
        ToDocumentsFolder(s.H);
        s.H.Press(Key.Enter);

        Assert.Equal([Opening], s.H.Press(Key.Enter));
        s.Loop!.Finish();
        Assert.Equal(1, s.Docs.Loads);
        Assert.IsType<DocumentReaderScreen>(s.H.Shell.Current);
    }

    [Fact]
    public void Escape_while_opening_leaves_and_drops_the_document()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,"), manualLoop: true);
        ToDocumentsFolder(s.H);
        s.H.Press(Key.Enter);

        Assert.Equal(["Deschide un document. Selectat: Documente, 1 din 1."], s.H.Press(Key.Escape));
        var before = s.H.Output.Announcements.Count;
        s.Loop!.Advance(TimeSpan.FromSeconds(1));
        s.Loop.Finish();

        Assert.Equal(before, s.H.Output.Announcements.Count);
        Assert.Null(Assert.IsType<FileBrowserScreen>(s.H.Shell.Current).Folder);
        Assert.Empty(s.Recent);
    }

    [Fact]
    public void Escape_while_opening_from_the_recent_list_drops_the_document_too()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,"), manualLoop: true);
        ToDocumentsFolder(s.H);
        s.H.Press(Key.Enter);
        s.Loop!.Finish();
        s.H.Press(Key.Escape);

        Assert.Empty(OpenFromRecents(s.H));
        Assert.Equal([string.Format(null, RootReturn, "Documente recente", 2)], s.H.Press(Key.Escape));
        var before = s.H.Output.Announcements.Count;
        s.Loop.Finish();

        Assert.Equal(before, s.H.Output.Announcements.Count);
        Assert.IsType<MenuScreen>(s.H.Shell.Current);
    }

    // --- Problems ---

    [Theory]
    [InlineData(DocumentProblem.NotFound, "Documentul nu a fost găsit.")]
    [InlineData(DocumentProblem.TooLarge, "Documentul este prea mare.")]
    [InlineData(DocumentProblem.Protected, "Documentul este protejat și nu poate fi deschis.")]
    [InlineData(DocumentProblem.Damaged, "Documentul nu a putut fi deschis. Poate fi deteriorat.")]
    [InlineData(DocumentProblem.Unsupported, "Documentul nu a putut fi deschis. Poate fi deteriorat.")]
    [InlineData(DocumentProblem.NoAccess, "HASI nu are voie să deschidă acest document.")]
    public void A_problem_is_said_with_the_error_sound_and_the_user_stays_in_the_folder(DocumentProblem problem, string expected)
    {
        var path = InDocuments("Carte.pdf");
        var s = OnDocumentsMenu(docs =>
        {
            docs.Add(path, Pdf(2));
            docs.LoadProblems[path] = problem;
        });

        Assert.Equal([expected], OpenFromDocuments(s.H));
        Assert.Equal(Earcon.Error, s.H.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(Documents, Assert.IsType<FileBrowserScreen>(s.H.Shell.Current).Folder);
        Assert.Empty(s.Recent);
    }

    [Fact]
    public void A_pdf_of_scanned_pages_only_is_not_opened_and_not_kept_among_the_recent_ones()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scan.pdf"), Pdf(2, 1, 2)));

        Assert.Equal(["Documentul conține doar imagini scanate. HASI nu poate citi încă imagini."], OpenFromDocuments(s.H));
        Assert.Equal(Earcon.Error, s.H.LastAnnouncement!.Utterance.Earcon);
        Assert.IsType<FileBrowserScreen>(s.H.Shell.Current);
        Assert.Empty(s.Recent);
    }

    // --- What is special, said once before the first line ---

    [Theory]
    [InlineData(new[] { 3 }, "Manual. Pagina 3 este o imagine scanată. Pagina 1.")]
    [InlineData(new[] { 3, 4, 7 }, "Manual. Paginile 3, 4 și 7 sunt imagini scanate. Pagina 1.")]
    [InlineData(new[] { 2, 3, 4, 5, 6 }, "Manual. Paginile 2, 3, 4, 5 și 6 sunt imagini scanate. Pagina 1.")]
    [InlineData(new[] { 2, 3, 4, 5, 6, 7 }, "Manual. 6 pagini sunt imagini scanate. Pagina 1.")]
    public void Scanned_pages_are_named_once_on_opening(int[] scanned, string expected)
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Manual.pdf"), Pdf(8, scanned)));

        Assert.Equal([expected], OpenFromDocuments(s.H));
        Assert.Equal(Earcon.None, s.H.LastAnnouncement!.Utterance.Earcon);
        s.H.Press(Key.F10);
        Assert.Equal(["Manual. Pagina 1."], s.H.Press(Key.Escape));
    }

    [Fact]
    public void Text_that_looks_broken_is_said_once_with_the_error_sound()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Manual.pdf"), Pdf(2) with { LooksGarbled = true }));

        Assert.Equal(["Manual. Textul acestui document pare stricat; unele litere pot fi citite greșit. Pagina 1."], OpenFromDocuments(s.H));
        Assert.Equal(Earcon.Error, s.H.LastAnnouncement!.Utterance.Earcon);
        s.H.Press(Key.F10);
        Assert.Equal(["Manual. Pagina 1."], s.H.Press(Key.Escape));
        Assert.Equal(Earcon.None, s.H.LastAnnouncement!.Utterance.Earcon);
    }

    // --- Resuming where the user stopped ---

    [Fact]
    public void A_pdf_reopened_from_the_recent_list_continues_from_its_page()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Manual.pdf"), Pdf(20)));
        OpenFromDocuments(s.H);
        for (var i = 0; i < 10; i++) s.H.Press(Key.PageDown);
        Assert.Equal(["Pagina 12 din 20. Pagina 12."], s.H.Press(Key.PageDown));
        s.H.Press(Key.Escape);

        var recent = Assert.Single(s.Recent);
        Assert.Equal((new ReadingPosition(11, 0), 12), (recent.Position, recent.Page));
        Assert.Equal(["Documente recente, 2 din 2.", "Documente recente. 1 document. Selectat: Manual, pagina 12, 1 din 1."],
            s.H.Press(Key.End, Key.Enter));
        Assert.Equal(["Manual. Continui de la pagina 12. Pagina 12."], s.H.Press(Key.Enter));
        Assert.Equal(new ReadingPosition(11, 0), s.Reader.Reader.CurrentPosition);
    }

    [Fact]
    public void A_book_opened_again_from_the_browser_continues_from_its_chapter()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Carte.epub"), Book()));
        OpenFromDocuments(s.H);
        s.H.Press(Key.PageDown, Key.PageDown, Key.Escape);

        Assert.Equal(["Carte. Continui de la capitolul 3. Titlu 1: Trei"], OpenFromDocuments(s.H));
        Assert.Equal("Carte", Assert.Single(s.Recent).Title);
    }

    [Fact]
    public void Other_documents_continue_from_the_start_of_the_sentence_where_the_user_stopped()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu doi. Trei patru cinci."));
        OpenFromDocuments(s.H);
        s.H.Press(CtrlRight, CtrlRight, CtrlRight, Key.Escape);
        Assert.Equal(new ReadingPosition(0, 14), Assert.Single(s.Recent).Position);

        Assert.Equal(["Scrisoare. Continui de unde ați rămas. Unu doi. Trei patru cinci."], OpenFromDocuments(s.H));
        Assert.Equal(new ReadingPosition(0, 9), s.Reader.Reader.CurrentPosition);
    }

    [Fact]
    public void A_document_left_at_its_beginning_opens_without_saying_it_continues()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Dragă Ana,\nCe mai faci?"));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Down, Key.Up, Key.Escape);

        Assert.Equal(["Scrisoare. Dragă Ana,"], OpenFromRecents(s.H));
    }

    [Fact]
    public void A_saved_position_beyond_a_shorter_document_is_brought_inside()
    {
        var path = InDocuments("Scrisoare.txt");
        var s = OnDocumentsMenu(docs => docs.Add(path, "Unu.\nDoi.\nTrei."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Down, Key.Down, Key.Escape);
        s.Docs.Add(path, "Unu. Doi.\nTrei patru.");

        Assert.Equal(["Scrisoare. Continui de unde ați rămas. Trei patru."], OpenFromRecents(s.H));
    }

    // --- Saving the position ---

    [Fact]
    public void The_position_is_saved_at_most_once_every_five_seconds_while_it_changes()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu.\nDoi.\nTrei."));
        OpenFromDocuments(s.H);
        var writes = s.H.UserData.Writes;

        s.H.Press(Key.Down);
        s.H.Shell.ClockTick();
        Assert.Equal(writes, s.H.UserData.Writes);

        s.Time.Advance(TimeSpan.FromSeconds(5));
        s.H.Shell.ClockTick();
        Assert.Equal(writes + 1, s.H.UserData.Writes);
        Assert.Equal(new ReadingPosition(1, 0), Assert.Single(s.Recent).Position);

        s.H.Press(Key.Down);
        s.Time.Advance(TimeSpan.FromSeconds(4));
        s.H.Shell.ClockTick();
        Assert.Equal(writes + 1, s.H.UserData.Writes);
        s.Time.Advance(TimeSpan.FromSeconds(1));
        s.H.Shell.ClockTick();
        Assert.Equal(writes + 2, s.H.UserData.Writes);
        Assert.Equal(new ReadingPosition(2, 0), Assert.Single(s.Recent).Position);

        s.Time.Advance(TimeSpan.FromSeconds(10));
        s.H.Shell.ClockTick();
        Assert.Equal(writes + 2, s.H.UserData.Writes); // nothing changed
    }

    [Fact]
    public void Leaving_the_reader_saves_the_position_at_once()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu.\nDoi."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Down);

        s.H.Press(Key.Escape);

        Assert.Equal(new ReadingPosition(1, 0), Assert.Single(s.Recent).Position);
    }

    [Fact]
    public void A_position_that_could_not_be_saved_is_tried_again()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu.\nDoi."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Down);
        s.H.UserData.FailWrites = true;
        s.Time.Advance(TimeSpan.FromSeconds(5));
        s.H.Shell.ClockTick();
        s.H.UserData.FailWrites = false;

        s.H.Shell.ClockTick();

        Assert.Equal(new ReadingPosition(1, 0), Assert.Single(s.Recent).Position);
    }

    // --- The document's menu ---

    [Fact]
    public void The_menu_of_a_text_document_has_no_page_choice_and_closes_the_document()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu.\nDoi."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Down);

        Assert.Equal(["Meniul documentului. 3 opțiuni. Selectat: Citește tot, 1 din 3."], s.H.Press(new KeyChord(Key.M, KeyModifiers.Ctrl)));
        Assert.Equal(["Spune poziția, 2 din 3.", "Închide documentul, 3 din 3."], s.H.Press(Key.Down, Key.Down));
        Assert.Equal([string.Format(null, RootReturn, "Deschide un document", 1)], s.H.Press(Key.Enter));
        Assert.Equal(new ReadingPosition(1, 0), Assert.Single(s.Recent).Position);
    }

    [Fact]
    public void Read_all_from_the_menu_reads_to_the_end_in_the_document()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Scrisoare.txt"), "Unu. Doi.\nTrei."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.F10);

        Assert.Equal(["Unu.", "Doi."], s.H.Press(Key.Enter));
        Assert.IsType<DocumentReaderScreen>(s.H.Shell.Current);
        Assert.True(s.Reader.Reader.IsReadingAll);
        Assert.Equal(["Trei.", "Sfârșitul documentului."], s.H.FinishAllSpeech());
        Assert.False(s.Reader.Reader.IsReadingAll);
    }

    [Fact]
    public void Say_position_from_the_menu_names_the_page_of_a_pdf()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Manual.pdf"), Pdf(2)));
        OpenFromDocuments(s.H);
        s.H.Press(Key.F10);

        Assert.Equal(["Spune poziția, 2 din 4.", "Rândul 1 din 2. Pagina 1 din 2."], s.H.Press(Key.Down, Key.Enter));
        Assert.IsType<DocumentReaderScreen>(s.H.Shell.Current);
    }

    [Fact]
    public void Go_to_page_takes_a_number_from_one_to_the_last_page()
    {
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Manual.pdf"), Pdf(20, 9)));
        OpenFromDocuments(s.H);
        s.H.Press(Key.F10, Key.Down);

        Assert.Equal(["Mergi la pagina…, 3 din 4.", "Mergi la pagina. Scrieți numărul paginii, de la 1 la 20, și apăsați Enter."],
            s.H.Press(Key.Down, Key.Enter));
        s.H.Type("21");
        Assert.Equal(["Scrieți un număr între 1 și 20."], s.H.Press(Key.Enter));
        s.H.Press(Key.Backspace, Key.Backspace);
        s.H.Type("a");
        Assert.Equal(["Scrieți un număr între 1 și 20."], s.H.Press(Key.Enter));
        s.H.Press(Key.Backspace);
        s.H.Type("0");
        Assert.Equal(["Scrieți un număr între 1 și 20."], s.H.Press(Key.Enter));
        s.H.Press(Key.Backspace);
        s.H.Type("7");

        Assert.Equal(["Pagina 7 din 20. Pagina 7."], s.H.Press(Key.Enter));
        Assert.Equal(new ReadingPosition(6, 0), s.Reader.Reader.CurrentPosition);
        Assert.Equal(["Pagina 8 din 20. Pagina 8."], s.H.Press(Key.PageDown));

        s.H.Press(Key.F10, Key.Down, Key.Down, Key.Enter);
        s.H.Type("9");
        Assert.Equal(["Pagina 9 este o imagine scanată. HASI nu poate citi încă imagini."], s.H.Press(Key.Enter));
        Assert.IsType<DocumentReaderScreen>(s.H.Shell.Current);
    }

    // --- The recent list ---

    [Fact]
    public void The_recent_list_keeps_the_last_ten_documents_newest_first()
    {
        var s = OnDocumentsMenu(docs =>
        {
            for (var i = 1; i <= 11; i++) docs.Add(InDocuments($"D{i:00}.txt"), $"Documentul {i}.");
        });
        for (var i = 1; i <= 11; i++)
        {
            OpenFromDocuments(s.H, i);
            s.H.Press(Key.Escape);
            s.Time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(["Documente recente, 2 din 2.", "Documente recente. 10 documente. Selectat: D11, 1 din 10."], s.H.Press(Key.End, Key.Enter));
        Assert.Equal(["D02, 10 din 10."], s.H.Press(Key.End));
        Assert.Equal(10, s.Recent.Count);
    }

    [Fact]
    public void A_document_no_longer_on_the_disk_leaves_the_recent_list()
    {
        var first = InDocuments("Alfa.txt");
        var second = InDocuments("Beta.txt");
        var s = OnDocumentsMenu(docs => docs.Add(first, "Unu.").Add(second, "Doi."));
        OpenFromDocuments(s.H, 1);
        s.H.Press(Key.Escape);
        OpenFromDocuments(s.H, 2);
        s.H.Press(Key.Escape);
        s.Docs.LoadProblems[first] = DocumentProblem.NotFound;
        s.Docs.LoadProblems[second] = DocumentProblem.NotFound;

        Assert.Equal(["Documentul nu mai există în locul în care era. Beta, 1 din 1."], OpenFromRecents(s.H, 2));
        Assert.Equal(Earcon.Error, s.H.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(second, Assert.Single(s.Recent).Path);

        Assert.Equal(["Documentul nu mai există în locul în care era. " + string.Format(null, RootReturn, "Documente recente", 2)],
            s.H.Press(Key.Enter));
        Assert.Empty(s.Recent);
    }

    [Fact]
    public void Another_problem_keeps_the_document_in_the_recent_list()
    {
        var path = InDocuments("Alfa.txt");
        var s = OnDocumentsMenu(docs => docs.Add(path, "Unu."));
        OpenFromDocuments(s.H);
        s.H.Press(Key.Escape);
        s.Docs.LoadProblems[path] = DocumentProblem.NoAccess;

        Assert.Equal(["HASI nu are voie să deschidă acest document."], OpenFromRecents(s.H));
        Assert.Single(s.Recent);
        Assert.Equal("Documente recente", s.H.Shell.Current.Title);
    }

    // --- Privacy ---

    [Fact]
    public void The_diagnostic_log_has_no_document_text_and_no_document_name()
    {
        var lines = new StringWriter();
        var s = OnDocumentsMenu(docs => docs.Add(InDocuments("Secretul meu.pdf"), FakeDocumentFormats.Pdf("Taina mea. Alt rând.", null)),
            manualLoop: true, log: new TextDiagnosticLog(lines, TimeProvider.System));
        ToDocumentsFolder(s.H);
        s.H.Press(Key.Enter);
        s.Loop!.Advance(TimeSpan.FromSeconds(1));
        s.Loop.Finish();
        s.H.Press(Key.Enter, Key.Right, Key.PageDown, Key.F10, Key.Down, Key.Enter, Key.F3, Key.F2, Key.Escape);
        s.H.Press(Key.End, Key.Enter, Key.Enter);
        s.Loop.Finish();
        s.H.Press(Key.F10, Key.Down, Key.Down, Key.Enter);
        s.H.Type("2");
        s.H.Press(Key.Enter, Key.Escape);

        var log = lines.ToString();
        Assert.Contains("Citire documente", log, StringComparison.Ordinal);
        Assert.Contains("JOB", log, StringComparison.Ordinal);
        Assert.Contains("CHAR   '*'", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Taina", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Pagina", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Continui", log, StringComparison.Ordinal);
    }
}
