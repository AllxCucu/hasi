using Hasi.Core.Diagnostics;
using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Modules.Editor;
using Hasi.Modules.Exit;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

// Inside the namespace: the enum must win over Open XML's root namespace DocumentFormat (Hasi.Documents is referenced).
using Hasi.Core.Documents;

/// <summary>Module 2, the text editor, with the real Romanian texts, a fake disk and documents in memory.</summary>
public class EditorModuleTests
{
    private const string Documents = @"C:\Users\Ana\Documents";
    private const string Desktop = @"C:\Users\Ana\Desktop";

    private static readonly KeyChord CtrlS = new(Key.S, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlEnd = new(Key.End, KeyModifiers.Ctrl);
    private static readonly KeyChord AltF4 = new(Key.F4, KeyModifiers.Alt);

    private const string NameField =
        "Numele documentului. Scrieți numele documentului și apăsați Enter. Dacă apăsați Enter fără să scrieți, se folosește numele propus: {0}.";

    private const string LeaveQuestion =
        "Documentul are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și ieși, 1 din 3.";

    private const string EditorMenuReturn = "Editor de text. Selectat: {0}, {1} din 3.";

    private static ManualTimeProvider Clock() =>
        new(new DateTimeOffset(2026, 10, 24, 11, 32, 0, TimeSpan.Zero), TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));

    /// <summary>HASI with the editor and Ieșire, on the editor's menu; Documente and Desktop on the disk.</summary>
    private static (ShellHarness H, FakeDocumentFormats Docs) OnEditorMenu(
        Action<FakeDocumentFormats>? documents = null,
        TimeProvider? time = null,
        IDiagnosticLog? log = null)
    {
        var docs = new FakeDocumentFormats();
        var h = new ShellHarness(time: time, diagnosticLog: log, documents: docs);
        h.Files.AddPlace(PlaceKind.Documents, Documents).AddPlace(PlaceKind.Desktop, Desktop);
        documents?.Invoke(docs);
        h.StartWithModules(new EditorModule(), new ExitModule());
        h.Press(Key.Enter);
        return (h, docs);
    }

    /// <summary>A new document with <paramref name="text"/> typed in it.</summary>
    private static (ShellHarness H, FakeDocumentFormats Docs, EditorScreen Editor) NewDocument(
        string text, Action<FakeDocumentFormats>? documents = null, TimeProvider? time = null, IDiagnosticLog? log = null)
    {
        var (h, docs) = OnEditorMenu(documents, time, log);
        h.Press(Key.Enter);
        TypeLines(h, text);
        return (h, docs, Assert.IsType<EditorScreen>(h.Shell.Current));
    }

    /// <summary>Types the text; each '\n' is the Enter key, as on the keyboard.</summary>
    private static void TypeLines(ShellHarness h, string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) h.Press(Key.Enter);
            h.Type(lines[i]);
        }
    }

    /// <summary>Opens a document from Documente through "Deschide un document" (the files are in Romanian order).</summary>
    private static EditorScreen OpenFromDocuments(ShellHarness h, int position = 1)
    {
        h.Press(Key.Home, Key.Down, Key.Enter, Key.Enter);
        for (var i = 1; i < position; i++) h.Press(Key.Down);
        h.Press(Key.Enter);
        return Assert.IsType<EditorScreen>(h.Shell.Current);
    }

    // --- New document, saving with the proposed name, recent documents ---

    [Fact]
    public void Root_menu_offers_three_choices()
    {
        var (h, _) = OnEditorMenu();

        Assert.Equal("Editor de text. 3 opțiuni. Selectat: Document nou, 1 din 3.", h.LastSaid);
        Assert.Equal(["Deschide un document, 2 din 3.", "Documente recente, 3 din 3."], h.Press(Key.Down, Key.Down));
    }

    [Fact]
    public void No_recent_documents_is_said_without_leaving_the_menu()
    {
        var (h, _) = OnEditorMenu();

        Assert.Equal(["Documente recente, 3 din 3.", "Nu aveți documente recente."], h.Press(Key.End, Key.Enter));
        Assert.IsType<MenuScreen>(h.Shell.Current);
    }

    [Fact]
    public void A_new_document_is_saved_in_documents_with_the_proposed_name_and_reopens_from_recents()
    {
        var (h, docs, editor) = NewDocument("Dragă Ana, ce mai faci azi?");

        Assert.Equal([string.Format(null, NameField, "Dragă Ana, ce mai faci")], h.Press(CtrlS));
        Assert.Equal(["Documentul a fost salvat: Dragă Ana, ce mai faci."], h.Press(Key.Enter));

        var path = $@"{Documents}\Dragă Ana, ce mai faci.docx";
        Assert.Same(editor, h.Shell.Current);
        Assert.Equal("Dragă Ana, ce mai faci azi?", docs.TextOf(path));
        Assert.Equal(DocumentFormat.Word, docs.Get(path)!.Format);
        Assert.Equal((path, "Dragă Ana, ce mai faci", DocumentFormat.Word, true), (editor.FilePath, editor.Title, editor.Format, editor.MadeByHasi));
        Assert.False(editor.HasUnsavedChanges);

        Assert.Equal([string.Format(null, EditorMenuReturn, "Document nou", 1)], h.Press(Key.Escape));
        Assert.Equal(
            ["Documente recente, 3 din 3.", "Documente recente. 1 document. Selectat: Dragă Ana, ce mai faci, 1 din 1.",
                "Dragă Ana, ce mai faci. Dragă Ana, ce mai faci azi?"],
            h.Press(Key.End, Key.Enter, Key.Enter));
        Assert.IsType<EditorScreen>(h.Shell.Current);
        Assert.Equal(["Meniu principal", "Editor de text", "Dragă Ana, ce mai faci"], h.Shell.Stack.Select(s => s.Title));
    }

    [Fact]
    public void Saving_again_writes_over_the_document_without_asking()
    {
        var (h, docs, editor) = NewDocument("Lista");
        h.Press(CtrlS, Key.Enter);
        h.Type(" de cumpărături");

        Assert.Equal(["Documentul a fost salvat."], h.Press(CtrlS));
        Assert.Equal("Lista de cumpărături", docs.TextOf($@"{Documents}\Lista.docx"));
        Assert.Equal(2, docs.Saved.Count);
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void An_empty_document_is_proposed_as_document()
    {
        var (h, _, _) = NewDocument("");

        Assert.Equal([string.Format(null, NameField, "Document")], h.Press(CtrlS));
    }

    [Fact]
    public void The_proposal_takes_five_words_of_the_first_line_with_text_without_forbidden_characters()
    {
        var (h, _, _) = NewDocument("\n  \nRaport: <martie> / aprilie, ședința? din 2026 și restul");

        Assert.Equal([string.Format(null, NameField, "Raport martie aprilie, ședința din")], h.Press(CtrlS));
    }

    [Fact]
    public void A_long_proposal_is_cut_between_words_at_forty_characters()
    {
        var (h, _, _) = NewDocument("Programarea la medicul specialist cardiolog dimineața");

        Assert.Equal([string.Format(null, NameField, "Programarea la medicul specialist")], h.Press(CtrlS));
    }

    [Fact]
    public void A_typed_name_with_forbidden_characters_gets_spaces_instead()
    {
        var (h, docs, editor) = NewDocument("text");
        h.Press(CtrlS);
        h.Type("Ana/Ion: notă?");

        Assert.Equal(["Documentul a fost salvat: Ana Ion  notă."], h.Press(Key.Enter));
        Assert.Equal($@"{Documents}\Ana Ion  notă.docx", editor.FilePath);
        Assert.NotNull(docs.Get(editor.FilePath!));
    }

    [Fact]
    public void A_name_of_only_forbidden_characters_uses_the_proposal()
    {
        var (h, _, editor) = NewDocument("Bună ziua");
        h.Press(CtrlS);
        h.Type("???");

        Assert.Equal(["Documentul a fost salvat: Bună ziua."], h.Press(Key.Enter));
        Assert.Equal($@"{Documents}\Bună ziua.docx", editor.FilePath);
    }

    [Fact]
    public void A_typed_extension_is_not_doubled()
    {
        var (h, _, editor) = NewDocument("x");
        h.Press(CtrlS);
        h.Type("Scrisoare.docx");
        h.Press(Key.Enter);

        Assert.Equal($@"{Documents}\Scrisoare.docx", editor.FilePath);
    }

    // --- An existing name ---

    [Fact]
    public void An_existing_name_is_replaced_only_after_enter()
    {
        var (h, docs, editor) = NewDocument("Text nou", d => d.Add($@"{Documents}\Text nou.docx", "vechi", madeByHasi: true));

        h.Press(CtrlS);
        Assert.Equal(
            ["Există deja un document cu numele Text nou. Îl înlocuiți? Apăsați Enter pentru da sau Escape pentru alt nume."],
            h.Press(Key.Enter));
        Assert.Equal("vechi", docs.TextOf($@"{Documents}\Text nou.docx"));

        Assert.Equal(["Documentul a fost salvat: Text nou."], h.Press(Key.Enter));
        Assert.Equal("Text nou", docs.TextOf($@"{Documents}\Text nou.docx"));
        Assert.Same(editor, h.Shell.Current);
    }

    [Fact]
    public void Escape_on_the_replace_question_goes_back_to_the_name_for_another_one()
    {
        var (h, docs, editor) = NewDocument("Text nou", d => d.Add($@"{Documents}\Text nou.docx", "vechi", madeByHasi: true));
        h.Press(CtrlS, Key.Enter);

        Assert.Equal([string.Format(null, NameField, "Text nou")], h.Press(Key.Escape));
        Assert.Equal("vechi", docs.TextOf($@"{Documents}\Text nou.docx"));
        Assert.Empty(docs.Saved);

        h.Type("Alt text");
        Assert.Equal(["Documentul a fost salvat: Alt text."], h.Press(Key.Enter));
        Assert.Equal($@"{Documents}\Alt text.docx", editor.FilePath);
    }

    [Fact]
    public void Other_keys_on_the_replace_question_say_it_again()
    {
        var (h, _, _) = NewDocument("Text nou", d => d.Add($@"{Documents}\Text nou.docx", "vechi", madeByHasi: true));
        h.Press(CtrlS, Key.Enter);

        Assert.Equal(
            ["Există deja un document cu numele Text nou. Îl înlocuiți? Apăsați Enter pentru da sau Escape pentru alt nume."],
            h.Press(Key.Down));
    }

    // --- Documents written by other programs, text files ---

    [Fact]
    public void A_docx_from_another_program_is_saved_as_a_copy_and_the_original_is_never_written()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Scrisoare.docx", "Dragă Ion,\nSalut."));
        var editor = OpenFromDocuments(h);
        Assert.Equal("Scrisoare. Dragă Ion,", h.LastSaid);
        h.Press(CtrlEnd);
        h.Type(" Pa!");

        Assert.Equal(
            ["Documentul original are formatare pe care HASI nu o poate păstra. Îl salvez ca document nou, iar originalul rămâne neschimbat. "
                + string.Format(null, NameField, "Scrisoare – HASI")],
            h.Press(CtrlS));
        Assert.Equal(["Documentul a fost salvat: Scrisoare – HASI."], h.Press(Key.Enter));

        Assert.Equal("Dragă Ion,\nSalut.", docs.TextOf($@"{Documents}\Scrisoare.docx"));
        Assert.Equal("Dragă Ion,\nSalut. Pa!", docs.TextOf($@"{Documents}\Scrisoare – HASI.docx"));
        Assert.Equal([$@"{Documents}\Scrisoare – HASI.docx"], docs.Saved);
        Assert.Equal(("Scrisoare – HASI", true), (editor.Title, editor.MadeByHasi));

        // From now on the copy is HASI's own: saved over itself.
        h.Type("!");
        Assert.Equal(["Documentul a fost salvat."], h.Press(CtrlS));
    }

    [Fact]
    public void The_original_name_is_refused_even_when_typed()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Scrisoare.docx", "Dragă Ion,"));
        OpenFromDocuments(h);
        h.Type("x");
        h.Press(CtrlS);
        h.Type("Scrisoare");

        Assert.Equal(["Acesta este documentul original, pe care HASI nu îl modifică. Scrieți alt nume."], h.Press(Key.Enter));
        Assert.Empty(docs.Saved);
        Assert.Equal("Numele documentului", h.Shell.Current.Title);
    }

    [Fact]
    public void A_text_file_is_saved_over_itself()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Lista.txt", "pâine\nlapte"));
        var editor = OpenFromDocuments(h);
        h.Press(CtrlEnd);
        h.Press(Key.Enter);
        h.Type("ouă");

        Assert.Equal(["Documentul a fost salvat."], h.Press(CtrlS));
        Assert.Equal("pâine\nlapte\nouă", docs.TextOf($@"{Documents}\Lista.txt"));
        Assert.Equal(DocumentFormat.Text, docs.Get($@"{Documents}\Lista.txt")!.Format);
        Assert.Equal(($@"{Documents}\Lista.txt", "Lista", DocumentFormat.Text), (editor.FilePath, editor.Title, editor.Format));
    }

    // --- Opening ---

    [Theory]
    [InlineData(DocumentProblem.NotFound, "Documentul nu a fost găsit.")]
    [InlineData(DocumentProblem.TooLarge, "Documentul este prea mare pentru editor.")]
    [InlineData(DocumentProblem.Protected, "Documentul este protejat și nu poate fi deschis.")]
    [InlineData(DocumentProblem.Damaged, "Documentul nu a putut fi deschis. Poate fi deteriorat.")]
    [InlineData(DocumentProblem.NoAccess, "HASI nu are voie să deschidă acest document.")]
    public void A_document_that_cannot_be_opened_is_explained_and_the_browser_stays(DocumentProblem problem, string expected)
    {
        var path = $@"{Documents}\Scrisoare.docx";
        var (h, _) = OnEditorMenu(d =>
        {
            d.Add(path, "text");
            d.LoadProblems[path] = problem;
        });
        h.Press(Key.Down, Key.Enter, Key.Enter);

        Assert.Equal([expected], h.Press(Key.Enter));
        Assert.Equal(Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);
        Assert.IsType<FileBrowserScreen>(h.Shell.Current);
        Assert.Empty(h.UserData.Names);
    }

    [Fact]
    public void Only_text_and_word_documents_are_offered()
    {
        var (h, _) = OnEditorMenu(d => d.Add($@"{Documents}\Scrisoare.docx", "text"));
        h.Files.AddFile($@"{Documents}\Buget.xlsx");

        Assert.Equal(["Documente. 1 opțiune. Selectat: Scrisoare, document Word, 1 din 1."], h.Press(Key.Down, Key.Enter, Key.Enter).Skip(2));
    }

    [Fact]
    public void An_opened_document_goes_above_the_editor_menu_and_into_recents()
    {
        var (h, _) = OnEditorMenu(d => d.Add($@"{Documents}\Scrisoare.docx", "text"));
        OpenFromDocuments(h);

        Assert.Equal(["Meniu principal", "Editor de text", "Scrisoare"], h.Shell.Stack.Select(s => s.Title));
        Assert.Equal([string.Format(null, EditorMenuReturn, "Deschide un document", 2)], h.Press(Key.Escape));
        Assert.Equal(["Documente recente, 3 din 3.", "Documente recente. 1 document. Selectat: Scrisoare, 1 din 1."],
            h.Press(Key.Down, Key.Enter));
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
    }

    [Fact]
    public void Recents_are_newest_first_and_keep_ten()
    {
        var (h, _) = OnEditorMenu(d =>
        {
            for (var i = 0; i < 12; i++) d.Add($@"{Documents}\Doc{i:00}.txt", "text");
        });
        for (var i = 1; i <= 12; i++)
        {
            OpenFromDocuments(h, i);
            h.Press(Key.Escape);
        }

        Assert.Equal(["Documente recente, 3 din 3.", "Documente recente. 10 documente. Selectat: Doc11, 1 din 10."],
            h.Press(Key.Down, Key.Enter));
        Assert.Equal(["Doc02, 10 din 10."], h.Press(Key.End));
    }

    [Fact]
    public void A_recent_document_that_was_moved_is_said_and_removed()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Unu.txt", "1").Add($@"{Documents}\Doi.txt", "2"));
        OpenFromDocuments(h, 1); // Doi
        h.Press(Key.Escape);
        OpenFromDocuments(h, 2); // Unu
        h.Press(Key.Escape);
        docs.LoadProblems[$@"{Documents}\Unu.txt"] = DocumentProblem.NotFound;
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Documentul nu mai există în locul în care era. Doi, 1 din 1."], h.Press(Key.Enter));
        Assert.Equal(Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);

        docs.LoadProblems[$@"{Documents}\Doi.txt"] = DocumentProblem.NotFound;
        Assert.Equal(["Documentul nu mai există în locul în care era. " + string.Format(null, EditorMenuReturn, "Documente recente", 3)],
            h.Press(Key.Enter));
        Assert.Equal(["Nu aveți documente recente."], h.Press(Key.Enter));
    }

    // --- A save that fails ---

    [Theory]
    [InlineData(DocumentProblem.DiskFull, "Documentul nu a putut fi salvat. Discul este plin. Textul este păstrat în HASI.")]
    [InlineData(DocumentProblem.NoAccess, "Documentul nu a putut fi salvat. HASI nu are voie să scrie acolo. Textul este păstrat în HASI.")]
    [InlineData(DocumentProblem.NotFound, "Documentul nu a putut fi salvat. Locul nu mai există; poate stick-ul a fost scos. Textul este păstrat în HASI.")]
    [InlineData(DocumentProblem.Unsupported, "Documentul nu a putut fi salvat. Textul este păstrat în HASI.")]
    public void A_failed_save_keeps_the_text_and_stays_in_the_document(DocumentProblem problem, string expected)
    {
        var (h, docs, editor) = NewDocument("Text important");
        docs.SaveProblem = problem;
        h.Press(CtrlS);

        Assert.Equal([expected], h.Press(Key.Enter));
        Assert.Equal(Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Same(editor, h.Shell.Current);
        Assert.Equal("Text important", editor.Buffer.Text);
        Assert.True(editor.HasUnsavedChanges);
        Assert.Null(editor.FilePath);
    }

    [Fact]
    public void A_failed_save_on_leaving_does_not_leave()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Lista.txt", "pâine"));
        var editor = OpenFromDocuments(h);
        h.Type("x");
        docs.SaveProblem = DocumentProblem.DiskFull;

        Assert.Equal(["Documentul nu a putut fi salvat. Discul este plin. Textul este păstrat în HASI."], h.Press(Key.Escape, Key.Enter).Skip(1));
        Assert.Same(editor, h.Shell.Current);
        Assert.True(editor.HasUnsavedChanges);
    }

    // --- Escape with unsaved changes ---

    [Fact]
    public void Escape_menu_save_and_leave()
    {
        var (h, docs, _) = NewDocument("Notă");

        Assert.Equal([LeaveQuestion], h.Press(Key.Escape));
        Assert.Equal([string.Format(null, NameField, "Notă")], h.Press(Key.Enter));
        Assert.Equal(["Documentul a fost salvat: Notă. " + string.Format(null, EditorMenuReturn, "Document nou", 1)], h.Press(Key.Enter));
        Assert.Equal("Notă", docs.TextOf($@"{Documents}\Notă.docx"));
    }

    [Fact]
    public void Escape_menu_leave_without_saving_asks_first()
    {
        var (h, docs, _) = NewDocument("Notă");
        h.Press(Key.Escape);

        Assert.Equal(
            ["Ieși fără să salvezi, 2 din 3.", "Modificările se vor pierde. Apăsați Enter pentru da sau Escape pentru nu."],
            h.Press(Key.Down, Key.Enter));
        Assert.Equal([string.Format(null, EditorMenuReturn, "Document nou", 1)], h.Press(Key.Enter));
        Assert.Empty(docs.Saved);
    }

    [Fact]
    public void Escape_menu_leave_without_saving_then_escape_stays_on_the_question()
    {
        var (h, _, editor) = NewDocument("Notă");
        h.Press(Key.Escape, Key.Down, Key.Enter);

        Assert.Equal(["Anulat. Modificări nesalvate. Selectat: Ieși fără să salvezi, 2 din 3."], h.Press(Key.Escape));
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void Escape_menu_stay_in_the_document()
    {
        var (h, _, editor) = NewDocument("Notă");
        h.Press(Key.Escape);

        Assert.Equal(["Rămâi în document, 3 din 3.", "Document nou. Notă"], h.Press(Key.End, Key.Enter));
        Assert.Same(editor, h.Shell.Current);
        Assert.Equal(["Document nou. Notă"], h.Press(Key.Escape, Key.Escape).Skip(1));
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void Help_on_the_leave_question()
    {
        var (h, _, _) = NewDocument("Notă");
        h.Press(Key.Escape);

        Assert.EndsWith(
            "Salvează și ieși păstrează modificările și închide documentul. Ieși fără să salvezi pierde modificările, după o confirmare. Rămâi în document, sau tasta Escape, vă întoarce în document.",
            h.Press(Key.F1).Single(), StringComparison.Ordinal);
    }

    // --- The document's menu ---

    [Fact]
    public void Document_menu_says_the_position_and_goes_back_to_the_text()
    {
        var (h, _, editor) = NewDocument("unu\ndoi");

        Assert.Equal(["Meniul documentului. 4 opțiuni. Selectat: Salvează, 1 din 4."], h.Press(Key.F10));
        Assert.Equal(["Spune poziția, 3 din 4.", "Rândul 2 din 2, coloana 4."], h.Press(Key.End, Key.Up, Key.Enter).Skip(1));
        Assert.Same(editor, h.Shell.Current);
    }

    [Fact]
    public void Document_menu_save_returns_to_the_text()
    {
        var (h, _, editor) = NewDocument("Notă");
        h.Press(new KeyChord(Key.M, KeyModifiers.Ctrl), Key.Enter);

        Assert.Equal(["Documentul a fost salvat: Notă."], h.Press(Key.Enter));
        Assert.Same(editor, h.Shell.Current);
    }

    [Fact]
    public void Document_menu_close_asks_about_unsaved_changes()
    {
        var (h, _, _) = NewDocument("Notă");
        h.Press(Key.F10);

        Assert.Equal(["Închide documentul, 4 din 4.", LeaveQuestion], h.Press(Key.End, Key.Enter));
        Assert.Equal(["Meniu principal", "Editor de text", "Document nou", "Modificări nesalvate"], h.Shell.Stack.Select(s => s.Title));
    }

    [Fact]
    public void Document_menu_close_leaves_a_saved_document()
    {
        var (h, _, _) = NewDocument("Notă");
        h.Press(CtrlS, Key.Enter, Key.F10);

        Assert.Equal([string.Format(null, EditorMenuReturn, "Document nou", 1)], h.Press(Key.End, Key.Enter).Skip(1));
    }

    [Fact]
    public void Save_a_copy_as_text_on_another_place_keeps_the_document_as_it_is()
    {
        var (h, docs, editor) = NewDocument("Rețetă de cozonac");
        h.Press(CtrlS, Key.Enter);
        h.Type(" bun");
        h.Press(Key.F10);

        Assert.Equal(["Salvează o copie, 2 din 4.", "Formatul copiei. 2 opțiuni. Selectat: Document Word, 1 din 2."], h.Press(Key.Down, Key.Enter));
        Assert.Equal(["Text, 2 din 2.", "Locul copiei. 2 opțiuni. Selectat: Documente, 1 din 2."], h.Press(Key.Down, Key.Enter));
        Assert.Equal(["Desktop, 2 din 2.", string.Format(null, NameField, "Rețetă de cozonac")], h.Press(Key.Down, Key.Enter));
        Assert.Equal(["Copia a fost salvată: Rețetă de cozonac."], h.Press(Key.Enter));

        Assert.Equal("Rețetă de cozonac bun", docs.TextOf($@"{Desktop}\Rețetă de cozonac.txt"));
        Assert.Equal(DocumentFormat.Text, docs.Get($@"{Desktop}\Rețetă de cozonac.txt")!.Format);
        Assert.Same(editor, h.Shell.Current);
        Assert.Equal($@"{Documents}\Rețetă de cozonac.docx", editor.FilePath);
        Assert.True(editor.HasUnsavedChanges);
    }

    // --- Closing HASI with unsaved work (Alt+F4) ---

    [Fact]
    public void Alt_F4_save_asks_the_name_then_closes()
    {
        var (h, docs, _) = NewDocument("Notă");

        Assert.Equal(
            ["Documentul Document nou are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3."],
            h.Press(AltF4));
        Assert.Equal([string.Format(null, NameField, "Notă")], h.Press(Key.Enter));
        Assert.Equal(["Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu."], h.Press(Key.Enter));
        Assert.Equal(["La revedere."], h.Press(Key.Enter));
        Assert.Equal("Notă", docs.TextOf($@"{Documents}\Notă.docx"));
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Alt_F4_save_of_a_document_with_a_path_closes_at_once()
    {
        var (h, docs) = OnEditorMenu(d => d.Add($@"{Documents}\Lista.txt", "pâine"));
        OpenFromDocuments(h);
        h.Type("x");

        Assert.Equal(["Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu."], h.Press(AltF4, Key.Enter).Skip(1));
        Assert.Equal("xpâine", docs.TextOf($@"{Documents}\Lista.txt"));
    }

    [Fact]
    public void Alt_F4_discard_closes_without_saving_and_deletes_the_draft()
    {
        var time = Clock();
        var (h, docs, editor) = NewDocument("Notă", time: time);
        time.Advance(TimeSpan.FromSeconds(30));
        h.Shell.ClockTick();
        Assert.Contains(DraftsName, h.UserData.Names);

        Assert.Equal(
            ["Continuă fără să salvezi, 2 din 3.", "Modificările nesalvate se vor pierde. Apăsați Enter pentru da sau Escape pentru nu.",
                "Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.", "La revedere."],
            h.Press(AltF4, Key.Down, Key.Enter, Key.Enter, Key.Enter).Skip(1));
        Assert.Empty(docs.Saved);
        Assert.False(editor.HasUnsavedChanges);
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Alt_F4_failed_save_keeps_hasi_open_in_the_document()
    {
        var (h, docs, editor) = NewDocument("Notă");
        docs.SaveProblem = DocumentProblem.DiskFull;

        Assert.Equal(["Documentul nu a putut fi salvat. Discul este plin. Textul este păstrat în HASI."],
            h.Press(AltF4, Key.Enter, Key.Enter).Skip(2));
        Assert.Same(editor, h.Shell.Current);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Windows_ending_the_session_keeps_a_draft_at_once()
    {
        var (h, _, _) = NewDocument("Notă", time: Clock());

        h.Shell.PowerSessionEnding();

        Assert.Contains(DraftsName, h.UserData.Names);
    }

    // --- Drafts ---

    private const string DraftsName = "editor-ciorne";
    private const string OldDraftName = "editor-ciorna";

    /// <summary>A draft as stored, read through a type of the test's own (the module's record is internal).</summary>
    private sealed record StoredDraft(
        string Id, string Title, string? Path, DocumentFormat Format, bool MadeByHasi, string Text, DateTimeOffset SavedAt);

    /// <summary>The single draft of the editor's first version, which had no id.</summary>
    private sealed record OldDraft(string Title, string? Path, DocumentFormat Format, bool MadeByHasi, string Text, DateTimeOffset SavedAt);

    private static List<StoredDraft> StoredDrafts(ShellHarness h) => h.UserData.Read<List<StoredDraft>>(DraftsName) ?? [];

    private static void Tick(ShellHarness h, ManualTimeProvider time, TimeSpan after)
    {
        time.Advance(after);
        h.Shell.ClockTick();
    }

    [Fact]
    public void A_draft_is_written_after_thirty_seconds_of_unsaved_changes_and_never_speaks()
    {
        var time = Clock();
        var (h, _, _) = NewDocument("Notă", time: time);
        var said = h.Output.Announcements.Count;

        for (var i = 0; i < 5; i++) Tick(h, time, TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(DraftsName, h.UserData.Names);

        Tick(h, time, TimeSpan.FromSeconds(5));
        var draft = Assert.Single(StoredDrafts(h));
        Assert.Equal(("Document nou", null, "Notă", time.GetUtcNow()), (draft.Title, draft.Path, draft.Text, draft.SavedAt.ToUniversalTime()));
        Assert.False(string.IsNullOrEmpty(draft.Id));
        Assert.Equal(said, h.Output.Announcements.Count);

        // Unchanged text: not written again.
        var writes = h.UserData.Writes;
        Tick(h, time, TimeSpan.FromMinutes(2));
        Assert.Equal(writes, h.UserData.Writes);

        // Changed: written again once 30 seconds passed since the last draft, over the same one.
        h.Type(" nouă");
        h.Shell.ClockTick();
        var again = Assert.Single(StoredDrafts(h));
        Assert.Equal((draft.Id, "Notă nouă"), (again.Id, again.Text));
    }

    [Fact]
    public void Saving_deletes_the_draft()
    {
        var time = Clock();
        var (h, _, _) = NewDocument("Notă", time: time);
        Tick(h, time, TimeSpan.FromSeconds(30));

        h.Press(CtrlS, Key.Enter);

        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    [Fact]
    public void A_saved_document_keeps_no_draft()
    {
        var time = Clock();
        var (h, _, _) = NewDocument("Notă", time: time);
        h.Press(CtrlS, Key.Enter);
        Tick(h, time, TimeSpan.FromMinutes(1));

        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    private static StoredDraft Draft(string? path = null, DocumentFormat format = DocumentFormat.Word) =>
        new("a", "Scrisoare", path, format, false, "Dragă Ana,\nCe faci?", new DateTimeOffset(2026, 10, 23, 18, 5, 0, TimeSpan.Zero));

    private static StoredDraft NewDocumentDraft(string id, string text, DateTimeOffset savedAt) =>
        new(id, "Document nou", null, DocumentFormat.Word, false, text, savedAt);

    /// <summary>Three drafts, stored out of order; newest first they are c, a, b.</summary>
    private static StoredDraft[] ThreeDrafts() =>
    [
        NewDocumentDraft("b", "Programarea la medic, joi la ora 10", new DateTimeOffset(2026, 10, 22, 7, 30, 0, TimeSpan.Zero)),
        Draft(),
        NewDocumentDraft("c", "Rețetă de cozonac", new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero)),
    ];

    private const string LabelA = "Dragă Ana, din 23 octombrie 2026, ora 21:05";
    private const string LabelB = "Programarea la medic, joi la, din 22 octombrie 2026, ora 10:30";
    private const string LabelC = "Rețetă de cozonac, din 24 octombrie 2026, ora 9:00";

    /// <summary>HASI started with drafts left in the user's data; the notice or the list is open.</summary>
    private static (ShellHarness H, FakeDocumentFormats Docs) StartedWithDrafts(
        IEnumerable<StoredDraft> drafts, Action<FakeDocumentFormats>? documents = null, ManualTimeProvider? time = null)
    {
        var docs = new FakeDocumentFormats();
        var h = new ShellHarness(time: time ?? Clock(), documents: docs);
        h.Files.AddPlace(PlaceKind.Documents, Documents);
        documents?.Invoke(docs);
        h.UserData.Write(DraftsName, drafts.ToList());
        h.StartWithModules(new EditorModule(), new ExitModule());
        return (h, docs);
    }

    private static (ShellHarness H, FakeDocumentFormats Docs) StartedWithDraft(StoredDraft draft, Action<FakeDocumentFormats>? documents = null) =>
        StartedWithDrafts([draft], documents);

    private const string DraftNotice =
        "Am găsit un document nesalvat: Scrisoare, din 23 octombrie 2026, ora 21:05. Ce doriți să faceți? 3 opțiuni. Selectat: Deschide documentul, 1 din 3.";

    private const string MainMenuReturn = "Meniu principal. Selectat: Editor de text, 1 din 2.";

    [Fact]
    public void At_startup_a_draft_is_offered_after_the_welcome()
    {
        var (h, _) = StartedWithDraft(Draft());

        Assert.Equal(["Bun venit în HASI. Meniu principal. 2 opțiuni. Selectat: Editor de text, 1 din 2.", DraftNotice],
            h.Output.Announcements.Select(a => a.Utterance.Text));
        Assert.Equal(UtterancePriority.Queue, h.LastAnnouncement!.Utterance.Priority);
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
    }

    [Theory]
    [InlineData("Dragă Ana,\nCe faci?", "Dragă Ana")]
    [InlineData("\n  \nUnu doi trei patru cinci șase", "Unu doi trei patru cinci")]
    [InlineData("Programarea la medicul specialist cardiolog dimineața", "Programarea la medicul specialist cardiolog")]
    [InlineData("", "Document nou")]
    public void A_new_document_draft_is_named_by_its_first_words(string text, string words)
    {
        var (h, _) = StartedWithDraft(NewDocumentDraft("a", text, new DateTimeOffset(2026, 10, 23, 18, 5, 0, TimeSpan.Zero)));

        Assert.Equal(
            $"Am găsit un document nesalvat: {words}, din 23 octombrie 2026, ora 21:05. Ce doriți să faceți? 3 opțiuni. Selectat: Deschide documentul, 1 din 3.",
            h.LastSaid);
    }

    [Fact]
    public void No_draft_no_notice()
    {
        var h = new ShellHarness(documents: new FakeDocumentFormats()).StartWithModules(new EditorModule());

        Assert.Single(h.Output.Announcements);
    }

    [Fact]
    public void Recovering_the_draft_opens_it_unsaved_with_its_path()
    {
        var path = $@"{Documents}\Scrisoare.txt";
        var (h, docs) = StartedWithDraft(Draft(path, DocumentFormat.Text), d => d.Add(path, "vechi"));

        Assert.Equal(["Scrisoare. Dragă Ana,"], h.Press(Key.Enter));
        var editor = Assert.IsType<EditorScreen>(h.Shell.Current);
        Assert.True(editor.HasUnsavedChanges);
        Assert.Equal((path, DocumentFormat.Text), (editor.FilePath, editor.Format));

        Assert.Equal(["Documentul a fost salvat."], h.Press(CtrlS));
        Assert.Equal("Dragă Ana,\nCe faci?", docs.TextOf(path));
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    [Fact]
    public void A_recovered_draft_asks_before_leaving_and_closing_it_unsaved_deletes_it()
    {
        var (h, _) = StartedWithDraft(Draft());
        h.Press(Key.Enter);

        Assert.Equal([LeaveQuestion], h.Press(Key.Escape));
        Assert.Equal([MainMenuReturn], h.Press(Key.Down, Key.Enter, Key.Enter).Skip(2));
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    [Fact]
    public void Deleting_the_draft_asks_first()
    {
        var (h, _) = StartedWithDraft(Draft());

        Assert.Equal(["Șterge ciorna, 2 din 3.", "Documentul nesalvat se va pierde. Apăsați Enter pentru da sau Escape pentru nu."],
            h.Press(Key.Down, Key.Enter));
        Assert.Contains(DraftsName, h.UserData.Names);
        Assert.Equal(["Ciorna a fost ștearsă. " + MainMenuReturn], h.Press(Key.Enter));
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    [Fact]
    public void Later_keeps_the_draft()
    {
        var (h, _) = StartedWithDraft(Draft());

        Assert.Equal(["Mai târziu, 3 din 3.", MainMenuReturn], h.Press(Key.End, Key.Enter));
        Assert.Contains(DraftsName, h.UserData.Names);
        Assert.Equal([MainMenuReturn], h.Press(Key.Enter, Key.Escape).Skip(1));
    }

    [Fact]
    public void Another_document_left_unchanged_does_not_delete_a_draft_kept_for_later()
    {
        var (h, _) = StartedWithDraft(Draft());
        h.Press(Key.Escape, Key.Enter, Key.Enter, Key.Escape);

        Assert.Equal(["a"], StoredDrafts(h).Select(d => d.Id));
    }

    [Fact]
    public void Two_unsaved_documents_keep_two_drafts_and_each_one_deletes_only_its_own()
    {
        var time = Clock();
        var (h, _) = StartedWithDrafts([Draft()], time: time);
        h.Press(Key.Escape, Key.Enter, Key.Enter); // later; the editor; a new document
        h.Type("Notă");
        Tick(h, time, TimeSpan.FromSeconds(30));

        var drafts = StoredDrafts(h);
        Assert.Equal(2, drafts.Count);
        Assert.Contains(Draft(), drafts);
        Assert.Contains(drafts, d => d.Text == "Notă" && d.Id != "a");

        // Saving the new document deletes its draft only.
        h.Press(CtrlS, Key.Enter);
        Assert.Equal([Draft()], StoredDrafts(h));

        // Leaving another one without saving, too.
        h.Press(Key.Escape, Key.Enter);
        h.Type("Altă notă");
        Tick(h, time, TimeSpan.FromSeconds(30));
        Assert.Equal(2, StoredDrafts(h).Count);
        h.Press(Key.Escape, Key.Down, Key.Enter, Key.Enter);
        Assert.Equal([Draft()], StoredDrafts(h));
    }

    // --- Several drafts at startup ---

    [Fact]
    public void At_startup_several_drafts_are_listed_newest_first()
    {
        var (h, _) = StartedWithDrafts(ThreeDrafts());

        Assert.Equal(
            ["Bun venit în HASI. Meniu principal. 2 opțiuni. Selectat: Editor de text, 1 din 2.",
                $"Am găsit 3 documente nesalvate. Selectat: {LabelC}, 1 din 3."],
            h.Output.Announcements.Select(a => a.Utterance.Text));
        Assert.Equal(UtterancePriority.Queue, h.LastAnnouncement!.Utterance.Priority);
        Assert.IsAssignableFrom<ISensitiveScreen>(h.Shell.Current);
        Assert.Equal("Documente nesalvate", h.Shell.Current.Title);
        Assert.Equal([$"{LabelA}, 2 din 3.", $"{LabelB}, 3 din 3.", $"Sfârșitul listei. {LabelB}, 3 din 3."], h.Press(Key.Down, Key.Down, Key.Down));
    }

    [Fact]
    public void Escape_on_the_list_keeps_every_draft()
    {
        var (h, _) = StartedWithDrafts(ThreeDrafts());

        Assert.Equal([MainMenuReturn], h.Press(Key.Escape));
        Assert.Equal(["a", "b", "c"], StoredDrafts(h).Select(d => d.Id).Order());
    }

    [Fact]
    public void Help_on_the_list_explains_it()
    {
        var (h, _) = StartedWithDrafts(ThreeDrafts());

        Assert.EndsWith(
            "Aici sunt documentele rămase nesalvate după o eroare sau o pană de curent, cel mai nou primul. Enter pe un document vă lasă să îl deschideți sau să ștergeți ciorna. Escape înseamnă mai târziu: toate ciornele sunt păstrate, iar HASI le oferă din nou la următoarea pornire. HASI păstrează cel mult 10 documente nesalvate; când apare al unsprezecelea, cel mai vechi se șterge.",
            h.Press(Key.F1).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_a_draft_from_the_list_takes_it_out_of_the_list_only()
    {
        var time = Clock();
        var (h, _) = StartedWithDrafts(ThreeDrafts(), time: time);

        Assert.Equal([$"{LabelC}. Ce doriți să faceți? 2 opțiuni. Selectat: Deschide documentul, 1 din 2."], h.Press(Key.Enter));
        Assert.Equal(["Document nou. Rețetă de cozonac"], h.Press(Key.Enter));
        var editor = Assert.IsType<EditorScreen>(h.Shell.Current);
        Assert.True(editor.HasUnsavedChanges);
        Assert.Equal(["Meniu principal", "Documente nesalvate", "Document nou"], h.Shell.Stack.Select(s => s.Title));

        // The recovered document writes over its own draft.
        h.Press(CtrlEnd);
        h.Type(" bun");
        Tick(h, time, TimeSpan.FromSeconds(30));
        var drafts = StoredDrafts(h);
        Assert.Equal(["a", "b", "c"], drafts.Select(d => d.Id).Order());
        Assert.Equal("Rețetă de cozonac bun", drafts.Single(d => d.Id == "c").Text);

        // Leaving it without saving deletes that draft and goes back to the list, without it.
        Assert.Equal([LeaveQuestion], h.Press(Key.Escape));
        Assert.Equal([$"Documente nesalvate. Selectat: {LabelA}, 1 din 2."], h.Press(Key.Down, Key.Enter, Key.Enter).Skip(2));
        Assert.Equal(["a", "b"], StoredDrafts(h).Select(d => d.Id).Order());
    }

    [Fact]
    public void Opening_the_last_draft_of_the_list_replaces_the_list()
    {
        var (h, _) = StartedWithDrafts([Draft(), NewDocumentDraft("c", "Rețetă de cozonac", new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero))]);
        h.Press(Key.Enter, Key.Enter); // opens c, above the list
        Assert.Equal(["Meniu principal", "Documente nesalvate", "Document nou"], h.Shell.Stack.Select(s => s.Title));
        h.Press(Key.Escape, Key.Down, Key.Enter, Key.Enter); // leaves it without saving: the list has one draft left

        Assert.Equal([$"{LabelA}. Ce doriți să faceți? 2 opțiuni. Selectat: Deschide documentul, 1 din 2."], h.Press(Key.Enter));
        Assert.Equal(["Scrisoare. Dragă Ana,"], h.Press(Key.Enter));
        Assert.Equal(["Meniu principal", "Scrisoare"], h.Shell.Stack.Select(s => s.Title));
    }

    [Fact]
    public void Deleting_a_draft_from_the_list_asks_first_and_keeps_the_others()
    {
        var (h, _) = StartedWithDrafts(ThreeDrafts());
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Șterge ciorna, 2 din 2.", "Documentul nesalvat se va pierde. Apăsați Enter pentru da sau Escape pentru nu."],
            h.Press(Key.Down, Key.Enter));
        Assert.Equal(["a", "b", "c"], StoredDrafts(h).Select(d => d.Id).Order());

        Assert.Equal([$"Ciorna a fost ștearsă. Documente nesalvate. Selectat: {LabelB}, 2 din 2."], h.Press(Key.Enter));
        Assert.Equal(["b", "c"], StoredDrafts(h).Select(d => d.Id).Order());
    }

    [Fact]
    public void Escape_on_a_draft_goes_back_to_the_list()
    {
        var (h, _) = StartedWithDrafts(ThreeDrafts());
        h.Press(Key.Enter);

        Assert.EndsWith("Escape vă întoarce la lista documentelor nesalvate.", h.Press(Key.F1).Single(), StringComparison.Ordinal);
        Assert.Equal([$"Documente nesalvate. Selectat: {LabelC}, 1 din 3."], h.Press(Key.Escape));
    }

    [Fact]
    public void Deleting_the_last_draft_closes_the_list()
    {
        var (h, _) = StartedWithDrafts([Draft(), NewDocumentDraft("c", "Rețetă de cozonac", new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero))]);

        Assert.Equal([$"Ciorna a fost ștearsă. Documente nesalvate. Selectat: {LabelA}, 1 din 1."],
            h.Press(Key.Enter, Key.Down, Key.Enter, Key.Enter).Skip(3));
        Assert.Equal(["Ciorna a fost ștearsă. " + MainMenuReturn], h.Press(Key.Enter, Key.Down, Key.Enter, Key.Enter).Skip(3));
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    // --- At most ten drafts ---

    [Fact]
    public void An_eleventh_draft_says_so_and_takes_the_place_of_the_oldest()
    {
        var time = Clock();
        var drafts = Enumerable.Range(0, 10).Select(i => NewDocumentDraft($"d{i}",
            i == 0 ? "Lista de cumpărături pentru sâmbătă dimineața" : $"Notă {i}",
            new DateTimeOffset(2026, 10, 14 + i, 8, 0, 0, TimeSpan.Zero)));
        var (h, _) = StartedWithDrafts(drafts, time: time);
        Assert.Equal("Am găsit 10 documente nesalvate. Selectat: Notă 9, din 23 octombrie 2026, ora 11:00, 1 din 10.", h.LastSaid);
        h.Press(Key.Escape, Key.Enter, Key.Enter); // later; the editor; a new document
        h.Type("A unsprezecea");
        var said = h.Output.Announcements.Count;

        Tick(h, time, TimeSpan.FromSeconds(30));

        Assert.Equal(said + 1, h.Output.Announcements.Count);
        var limit = h.LastAnnouncement!.Utterance;
        Assert.Equal(
            "Sunt deja 10 documente nesalvate păstrate. Cel mai vechi, Lista de cumpărături pentru sâmbătă, din 14 octombrie 2026, ora 11:00, se șterge.",
            limit.Text);
        Assert.Equal(UtterancePriority.Queue, limit.Priority);
        Assert.True(limit.Sensitive);
        var stored = StoredDrafts(h);
        Assert.Equal(10, stored.Count);
        Assert.DoesNotContain(stored, d => d.Id == "d0");
        Assert.Contains(stored, d => d.Text == "A unsprezecea");

        // The same document again: written over its own draft, nothing said, nothing else deleted.
        h.Type("!");
        said = h.Output.Announcements.Count;
        Tick(h, time, TimeSpan.FromSeconds(30));
        Assert.Equal(said, h.Output.Announcements.Count);
        Assert.Equal(10, StoredDrafts(h).Count);
        Assert.Contains(StoredDrafts(h), d => d.Id == "d1");
    }

    [Fact]
    public void A_draft_that_cannot_be_written_deletes_nothing_and_is_announced_once()
    {
        var time = Clock();
        var drafts = Enumerable.Range(0, 10).Select(i =>
            NewDocumentDraft($"d{i}", $"Notă {i}", new DateTimeOffset(2026, 10, 14 + i, 8, 0, 0, TimeSpan.Zero)));
        var (h, _) = StartedWithDrafts(drafts, time: time);
        h.Press(Key.Escape, Key.Enter, Key.Enter);
        h.Type("A unsprezecea");
        var said = h.Output.Announcements.Count;
        h.UserData.FailWrites = true;

        Tick(h, time, TimeSpan.FromSeconds(30));
        Tick(h, time, TimeSpan.FromSeconds(30));

        // The owner's rule: the sentence comes before the deletion, so it is heard once even if the write fails.
        Assert.Equal(said + 1, h.Output.Announcements.Count);
        Assert.StartsWith("Sunt deja 10 documente nesalvate păstrate.", h.Output.Announcements[^1].Utterance.Text, StringComparison.Ordinal);
        Assert.Contains(StoredDrafts(h), d => d.Id == "d0");
    }

    // --- The first version's single draft ---

    private static OldDraft FirstVersionDraft() =>
        new("Scrisoare", null, DocumentFormat.Word, false, "Dragă Ana,\nCe faci?", new DateTimeOffset(2026, 10, 23, 18, 5, 0, TimeSpan.Zero));

    [Fact]
    public void The_first_versions_draft_moves_into_the_list()
    {
        var h = new ShellHarness(time: Clock(), documents: new FakeDocumentFormats());
        h.UserData.Write(OldDraftName, FirstVersionDraft());
        h.StartWithModules(new EditorModule(), new ExitModule());

        Assert.Equal(DraftNotice, h.LastSaid);
        Assert.DoesNotContain(OldDraftName, h.UserData.Names);
        var moved = Assert.Single(StoredDrafts(h));
        Assert.False(string.IsNullOrEmpty(moved.Id));
        Assert.Equal(("Scrisoare", "Dragă Ana,\nCe faci?"), (moved.Title, moved.Text));

        // It is a draft like the others: deleting it removes it.
        h.Press(Key.Down, Key.Enter, Key.Enter);
        Assert.DoesNotContain(DraftsName, h.UserData.Names);
    }

    [Fact]
    public void The_first_versions_draft_joins_the_drafts_already_in_the_list()
    {
        var h = new ShellHarness(time: Clock(), documents: new FakeDocumentFormats());
        h.UserData.Write(DraftsName, new List<StoredDraft> { NewDocumentDraft("c", "Rețetă de cozonac", new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero)) });
        h.UserData.Write(OldDraftName, FirstVersionDraft());
        h.StartWithModules(new EditorModule(), new ExitModule());

        Assert.Equal($"Am găsit 2 documente nesalvate. Selectat: {LabelC}, 1 din 2.", h.LastSaid);
        Assert.Equal(2, StoredDrafts(h).Count);
        Assert.DoesNotContain(OldDraftName, h.UserData.Names);
    }

    // --- Privacy ---

    [Fact]
    public void The_diagnostic_log_has_no_typed_text_and_no_document_name()
    {
        var lines = new StringWriter();
        var (h, _, _) = NewDocument("Taina mea", log: new TextDiagnosticLog(lines, TimeProvider.System));
        h.Press(CtrlS);
        h.Type("Secretul");
        h.Press(Key.Enter, Key.F3, Key.F2, Key.Escape, Key.End, Key.Enter, Key.Enter);
        h.Type("x");
        h.Press(Key.Escape, Key.Down, Key.Enter, Key.Enter);

        var log = lines.ToString();
        Assert.Contains("CHAR   '*'", log, StringComparison.Ordinal);
        Assert.Contains("Editor de text", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Taina", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", log, StringComparison.Ordinal);
        Assert.DoesNotContain("'x'", log, StringComparison.Ordinal);
    }
}
