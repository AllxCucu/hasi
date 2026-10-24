using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

// Inside the namespace: the enum must win over Open XML's root namespace DocumentFormat (Hasi.Documents is referenced).
using Hasi.Core.Documents;

/// <summary>The document reader with the real Romanian resources: keys pressed, text heard, speech finished by the test.</summary>
public class TextReaderScreenTests
{
    private static readonly KeyChord ShiftH = new(Key.H, KeyModifiers.Shift);
    private static readonly KeyChord CtrlRight = new(Key.Right, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlEnd = new(Key.End, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlM = new(Key.M, KeyModifiers.Ctrl);

    private const string Unavailable = "Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H.";

    private static Document Text(params string[] paragraphs) =>
        new([.. paragraphs.Select(p => new DocumentParagraph(p))], DocumentFormat.Text, MadeByHasi: false);

    /// <summary>A Word document: "#2 Text" is a heading of level 2.</summary>
    private static Document Word(params string[] paragraphs) =>
        new([.. paragraphs.Select(Paragraph)], DocumentFormat.Word, MadeByHasi: false);

    private static DocumentParagraph Paragraph(string text) =>
        text.StartsWith('#') ? new DocumentParagraph(text[3..], HeadingLevel: text[1] - '0') : new DocumentParagraph(text);

    /// <summary>Three pages: page 1 and 3 with text, page 2 scanned (no text).</summary>
    private static Document Pdf() => new(
        [
            new DocumentParagraph("Prima pagină. Are două propoziții.", Page: 1),
            new DocumentParagraph("Tot pe prima.", Page: 1),
            new DocumentParagraph("A treia pagină.", Page: 3),
        ],
        DocumentFormat.Pdf, MadeByHasi: false)
    {
        PageCount = 3,
        ScannedPages = [2],
    };

    private static Document Epub() => new(
        [
            new DocumentParagraph("Început", HeadingLevel: 1, Chapter: 1),
            new DocumentParagraph("Era odată.", Chapter: 1),
            new DocumentParagraph("Apoi a fost.", Chapter: 2),
        ],
        DocumentFormat.Epub, MadeByHasi: false)
    {
        ChapterTitles = ["Început", ""],
    };

    private sealed class Opened(ShellHarness h, TextReaderScreen reader, List<ReadingPosition> positions)
    {
        public ShellHarness H { get; } = h;

        public TextReaderScreen Reader { get; } = reader;

        public List<ReadingPosition> Positions { get; } = positions;

        public void Deconstruct(out ShellHarness harness, out TextReaderScreen screen)
        {
            harness = H;
            screen = Reader;
        }
    }

    private static Opened Open(Document document, ReadingPosition? start = null, string? prefix = null,
        Func<IScreenContext, ScreenResult>? onMenu = null)
    {
        var positions = new List<ReadingPosition>();
        var reader = new TextReaderScreen("Carte", document, start, onMenu, positionChanged: positions.Add, entryPrefix: prefix);
        var h = new ShellHarness().Start(new MenuScreen("Documente", [new MenuItem("Carte", _ => ScreenResult.Push(reader))]));
        h.Press(Key.Enter);
        Assert.Same(reader, h.Shell.Current);
        return new Opened(h, reader, positions);
    }

    // --- Entering and moving ---

    [Fact]
    public void Entering_says_the_title_and_the_current_line()
    {
        var (h, _) = Open(Text("Dragă Ana,", "Ce mai faci?"), new ReadingPosition(1, 3));

        Assert.Equal("Carte. Ce mai faci?", h.LastSaid);
        Assert.True(h.LastAnnouncement!.Utterance.Sensitive);
    }

    [Fact]
    public void Entering_further_in_says_what_the_module_gives_after_the_title_only_the_first_time()
    {
        var (h, _) = Open(Text("unu", "doi"), new ReadingPosition(1, 0), prefix: "Continui de la rândul 2.",
            onMenu: _ => ScreenResult.Push(new MenuScreen("Meniu", [new MenuItem("a")])));

        Assert.Equal("Carte. Continui de la rândul 2. doi", h.LastSaid);
        h.Press(Key.F10);
        Assert.Equal(["Carte. doi"], h.Press(Key.Escape));
    }

    [Fact]
    public void A_start_position_outside_the_document_is_brought_inside()
    {
        var opened = Open(Text("unu", "doi"), new ReadingPosition(9, 99));

        Assert.Equal("Carte. doi", opened.H.LastSaid);
        Assert.Equal(new ReadingPosition(1, 3), opened.Reader.CurrentPosition);
    }

    [Fact]
    public void A_heading_line_says_its_level()
    {
        var (h, _) = Open(Word("#1 Rețete", "Cozonac"));

        Assert.Equal("Carte. Titlu 1: Rețete", h.LastSaid);
        Assert.Equal(["Cozonac"], h.Press(Key.Down));
        Assert.Equal(["Titlu 1: Rețete"], h.Press(Key.Up));
    }

    [Fact]
    public void The_moves_speak_like_the_editor()
    {
        var (h, _) = Open(Text("Bună ziua", "Pa"));

        Assert.Equal(["u"], h.Press(Key.Right));
        Assert.Equal(["ziua"], h.Press(CtrlRight));
        Assert.Equal(["Sfârșit de rând"], h.Press(Key.End));
        Assert.Equal(["B"], h.Press(Key.Home));
        Assert.Equal(["Pa"], h.Press(Key.Down));
        Assert.Equal(["Sfârșitul documentului."], h.Press(CtrlEnd));
        Assert.Equal(["Sfârșitul documentului."], h.Press(Key.Right));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["Începutul documentului. Bună ziua"], h.Press(new KeyChord(Key.Home, KeyModifiers.Ctrl)));
        Assert.Equal(["Începutul documentului."], h.Press(Key.Up));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void A_letter_with_a_combining_mark_is_one_character()
    {
        var (h, _) = Open(Text("așa"));

        Assert.Equal(["ș"], h.Press(Key.Right));
        Assert.Equal(["a"], h.Press(Key.Right));
    }

    [Fact]
    public void Nothing_can_be_typed_or_deleted()
    {
        var (h, reader) = Open(Text("unu"));

        Assert.Equal([Unavailable], h.Press(Key.A));
        Assert.Equal([Unavailable], h.Type("a"));
        Assert.Equal([Unavailable], h.Press(Key.Backspace));
        Assert.Equal([Unavailable], h.Press(Key.Delete));
        Assert.Equal(new ReadingPosition(0, 0), reader.CurrentPosition);
    }

    [Fact]
    public void Every_change_of_position_is_told_to_the_module()
    {
        var opened = Open(Text("unu", "doi"));

        opened.H.Press(Key.Right, Key.Down, Key.Left, Key.Right);

        Assert.Equal([new(0, 1), new(1, 1), new(1, 0), new(1, 1)], opened.Positions);
    }

    [Fact]
    public void Escape_closes_the_reader()
    {
        var (h, _) = Open(Text("unu"));

        Assert.Equal(["Documente. Selectat: Carte, 1 din 1."], h.Press(Key.Escape));
    }

    [Fact]
    public void F1_explains_the_keys()
    {
        var (h, _) = Open(Text("unu"));

        var help = h.Press(Key.F1).Single();

        Assert.Contains("Enter citește tot", help, StringComparison.Ordinal);
        Assert.Contains("Orice tastă oprește citirea", help, StringComparison.Ordinal);
        Assert.Contains("H duce la titlul următor, Shift H la titlul anterior", help, StringComparison.Ordinal);
        Assert.Contains("Page Down și Page Up", help, StringComparison.Ordinal);
    }

    // --- Headings, pages, chapters ---

    [Fact]
    public void H_and_shift_H_go_to_the_next_and_previous_heading()
    {
        var (h, _) = Open(Word("Introducere", "#1 Capitolul unu", "Text", "#2 Secțiune", "Mai mult text"));

        Assert.Equal(["Titlu 1: Capitolul unu"], h.Press(Key.H));
        Assert.Equal(["Titlu 2: Secțiune"], h.Press(Key.H));
        Assert.Equal(["Nu mai există titluri după aici."], h.Press(Key.H));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["Titlu 1: Capitolul unu"], h.Press(ShiftH));
        Assert.Equal(["Nu mai există titluri înainte de aici."], h.Press(ShiftH));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Page_down_on_a_word_document_goes_to_the_next_heading()
    {
        var (h, _) = Open(Word("Introducere", "#1 Capitolul unu", "Text"));

        Assert.Equal(["Titlu 1: Capitolul unu"], h.Press(Key.PageDown));
        Assert.Equal(["Nu mai există titluri după aici."], h.Press(Key.PageDown));
        Assert.Equal(["Nu mai există titluri înainte de aici."], h.Press(Key.PageUp));
    }

    [Fact]
    public void Page_down_on_a_pdf_says_the_page_and_its_first_line_and_names_a_scanned_page()
    {
        var (h, reader) = Open(Pdf());

        Assert.Equal(["Pagina 2 este o imagine scanată. HASI nu poate citi încă imagini."], h.Press(Key.PageDown));
        Assert.Equal("Rândul 3 din 3. Pagina 2 din 3.", reader.Position(h.Shell).Text);
        Assert.Equal(["Pagina 3 din 3. A treia pagină."], h.Press(Key.PageDown));
        Assert.Equal(["Aceasta este ultima pagină."], h.Press(Key.NumPad3));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["Pagina 2 este o imagine scanată. HASI nu poate citi încă imagini."], h.Press(Key.PageUp));
        Assert.Equal(["Pagina 1 din 3. Prima pagină. Are două propoziții."], h.Press(Key.NumPad9));
        Assert.Equal(["Aceasta este prima pagină."], h.Press(Key.PageUp));
    }

    [Fact]
    public void An_arrow_after_a_scanned_page_forgets_it()
    {
        var (h, reader) = Open(Pdf());
        h.Press(Key.PageDown);

        Assert.Equal(["Sfârșit de rând"], h.Press(Key.End));
        Assert.Equal("Rândul 3 din 3. Pagina 3 din 3.", reader.Position(h.Shell).Text);
    }

    [Fact]
    public void Page_down_on_an_epub_goes_to_the_next_chapter()
    {
        var (h, reader) = Open(Epub());

        Assert.Equal("Rândul 1 din 3. Capitolul 1 din 2.", reader.Position(h.Shell).Text);
        Assert.Equal(["Capitolul 2. Apoi a fost."], h.Press(Key.PageDown));
        Assert.Equal(["Acesta este ultimul capitol."], h.Press(Key.PageDown));
        Assert.Equal(["Capitolul 1: Început. Titlu 1: Început"], h.Press(Key.PageUp));
        Assert.Equal(["Acesta este primul capitol."], h.Press(Key.PageUp));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Position_of_a_text_document_says_only_the_line()
    {
        var (h, reader) = Open(Text("unu", "doi", "trei"), new ReadingPosition(1, 0));

        Assert.Equal("Rândul 2 din 3.", reader.Position(h.Shell).Text);
        Assert.True(reader.Position(h.Shell).Sensitive);
    }

    // --- Reading everything ---

    [Fact]
    public void Enter_reads_from_the_sentence_at_the_cursor_keeping_two_sentences_queued()
    {
        var opened = Open(Text("Unu. Doi. Trei.", "Patru."), new ReadingPosition(0, 6));
        var h = opened.H;

        Assert.Equal(["Doi.", "Trei."], h.Press(Key.Enter));
        Assert.Equal(2, h.PendingSpeech.Count);
        Assert.Equal([UtterancePriority.Interrupt, UtterancePriority.Queue], h.PendingSpeech.Select(a => a.Utterance.Priority));
        Assert.All(h.PendingSpeech, a => Assert.True(a.Utterance.Sensitive));
        Assert.Equal(new ReadingPosition(0, 5), opened.Reader.CurrentPosition);

        Assert.Equal(["Patru."], h.FinishSpeech());
        Assert.Equal(new ReadingPosition(0, 10), opened.Reader.CurrentPosition);
        Assert.Equal(2, h.PendingSpeech.Count);

        Assert.Empty(h.FinishSpeech());
        Assert.Equal(new ReadingPosition(1, 0), opened.Reader.CurrentPosition);
        Assert.Single(h.PendingSpeech);

        Assert.Equal(["Sfârșitul documentului."], h.FinishSpeech());
        Assert.False(opened.Reader.IsReadingAll);
        Assert.Equal(new ReadingPosition(1, 6), opened.Reader.CurrentPosition);
    }

    [Fact]
    public void A_key_stops_reading_and_the_cursor_stays_at_the_start_of_that_sentence_which_enter_repeats()
    {
        var (h, reader) = Open(Text("Unu. Doi. Trei. Patru."));
        h.Press(Key.Enter);
        h.FinishSpeech(); // "Unu." heard; "Doi." is being said

        Assert.Empty(h.Press(Key.Down)); // only stops
        Assert.False(reader.IsReadingAll);
        Assert.Empty(h.PendingSpeech);
        Assert.Equal(new ReadingPosition(0, 5), reader.CurrentPosition);

        Assert.Equal(["Doi.", "Trei."], h.Press(Key.Enter));
    }

    [Fact]
    public void Ctrl_stops_reading_at_the_start_of_the_sentence_being_said()
    {
        var (h, reader) = Open(Text("Unu. Doi. Trei."));
        h.Press(Key.Enter);
        h.FinishSpeech();

        Assert.Empty(h.Press(Key.LeftCtrl));

        Assert.False(reader.IsReadingAll);
        Assert.Equal(new ReadingPosition(0, 5), reader.CurrentPosition);
        Assert.Equal(["Doi.", "Trei."], h.Press(Key.Enter));
    }

    [Fact]
    public void A_global_key_stops_reading_and_still_does_its_job()
    {
        var (h, reader) = Open(Text("Unu. Doi."));
        h.Press(Key.Enter);

        Assert.Equal(["Sunteți în: Documente, Carte."], h.Press(Key.F3));
        Assert.False(reader.IsReadingAll);
        Assert.Equal(new ReadingPosition(0, 0), reader.CurrentPosition);
    }

    [Fact]
    public void Escape_while_reading_stops_and_leaves_with_the_position_kept()
    {
        var opened = Open(Text("Unu. Doi. Trei."));
        var h = opened.H;
        h.Press(Key.Enter);
        h.FinishSpeech();

        Assert.Equal(["Documente. Selectat: Carte, 1 din 1."], h.Press(Key.Escape));
        Assert.Equal(new ReadingPosition(0, 5), opened.Positions[^1]);
    }

    [Fact]
    public void Starting_again_ignores_the_reports_of_the_reading_before()
    {
        var (h, reader) = Open(Text("Unu. Doi. Trei."));
        h.Press(Key.Enter);

        // A menu asks to read all again while reading: the old sentences are cut off and reported Cancelled.
        h.Shell.Show(reader.ReadAll(h.Shell));
        h.DeliverSpeechReports();

        Assert.True(reader.IsReadingAll);
        Assert.Equal(["Unu.", "Doi."], h.PendingSpeech.Select(a => a.Utterance.Text));
        Assert.Equal(["Trei.", "Sfârșitul documentului."], h.FinishAllSpeech());
    }

    [Fact]
    public void A_voice_failure_stops_reading_and_says_so()
    {
        var (h, reader) = Open(Text("Unu. Doi. Trei."));
        h.Press(Key.Enter);
        h.FinishSpeech();

        Assert.Equal(["Citirea s-a oprit: vocea nu a putut rosti textul."], h.FinishSpeech(SpeechOutcome.Failed));
        Assert.Equal(Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);
        Assert.False(reader.IsReadingAll);
        Assert.Equal(new ReadingPosition(0, 5), reader.CurrentPosition);
        Assert.Empty(h.FinishAllSpeech()); // nothing more is read
    }

    [Fact]
    public void Enter_at_the_end_of_the_document_says_so()
    {
        var (h, reader) = Open(Text("Unu."), new ReadingPosition(0, 4));

        Assert.Equal(["Sfârșitul documentului."], h.Press(Key.Enter));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.False(reader.IsReadingAll);
    }

    [Fact]
    public void Reading_a_pdf_names_each_new_page()
    {
        var (h, _) = Open(Pdf());

        Assert.Equal(["Prima pagină.", "Are două propoziții."], h.Press(Key.Enter));
        Assert.Equal(["Tot pe prima.", "Pagina 3. A treia pagină.", "Sfârșitul documentului."], h.FinishAllSpeech());
    }

    [Fact]
    public void Reading_an_epub_names_each_new_chapter()
    {
        var (h, _) = Open(Epub());

        Assert.Equal(["Început", "Era odată."], h.Press(Key.Enter));
        Assert.Equal(["Capitolul 2. Apoi a fost.", "Sfârșitul documentului."], h.FinishAllSpeech());
    }

    [Fact]
    public void The_first_sentence_read_has_no_page_name_even_on_a_later_page()
    {
        var (h, _) = Open(Pdf(), new ReadingPosition(2, 0));

        Assert.Equal(["A treia pagină."], h.Press(Key.Enter));
    }

    [Fact]
    public void Ctrl_M_opens_the_menu_given_by_the_module()
    {
        var (h, _) = Open(Text("unu"), onMenu: _ => ScreenResult.Push(new MenuScreen("Meniul documentului", [new MenuItem("Spune poziția")])));

        Assert.Equal(["Meniul documentului. 1 opțiune. Selectat: Spune poziția, 1 din 1."], h.Press(CtrlM));
    }
}
