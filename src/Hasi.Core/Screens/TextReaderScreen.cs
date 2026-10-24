using Hasi.Core.Documents;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Text;

namespace Hasi.Core.Screens;

/// <summary>
/// Reading a document, without changing it: the editor's moves (character, word, line = paragraph, start and end),
/// headings named with their level, H / Shift+H to the next / previous heading, Page Down / Page Up to the next /
/// previous page of a PDF or chapter of an EPUB (heading in other documents), and Enter to read everything from the
/// cursor, sentence by sentence (<see cref="ContinuousReader"/>). Any key stops the reading; the cursor stays at the start
/// of the sentence that was being said, so Enter repeats it. The document's menu and leaving belong to whoever opened
/// the screen; it hears of every change of position, to keep it.
/// Everything it says is private content: masked in the diagnostic log, with the whole text shown on screen.
/// </summary>
public sealed class TextReaderScreen : IScreen, IHasKeyProfile, ISensitiveScreen, ISpeechListener
{
    private readonly Document _document;
    private readonly TextNavigator _navigator;
    private readonly ContinuousReader _reading;
    private readonly Func<IScreenContext, ScreenResult>? _onMenu;
    private readonly Func<IScreenContext, ScreenResult>? _onBack;
    private readonly Action<ReadingPosition>? _positionChanged;
    private readonly string? _entryPrefix;

    /// <summary>Where each paragraph starts in the buffer's text.</summary>
    private readonly int[] _paragraphStarts;

    private ReadingPosition _reported;
    private bool _entered;

    /// <summary>
    /// A PDF page without text the user went to with Page Down / Page Up: the cursor waits on the text after it, while
    /// the position and the next page key count from this page. Any other move forgets it.
    /// </summary>
    private int? _pageWithoutText;

    /// <param name="start">Where reading starts; null: the beginning. Clamped to the document.</param>
    /// <param name="onMenu">F10 / Ctrl+M; null: the key is unavailable.</param>
    /// <param name="onBack">Escape; null: the screen closes.</param>
    /// <param name="positionChanged">Told every new position (moves, pages, reading aloud).</param>
    /// <param name="entryPrefix">Said after the title on opening, for example "Continui de la pagina 12."</param>
    public TextReaderScreen(string title, Document document, ReadingPosition? start = null,
        Func<IScreenContext, ScreenResult>? onMenu = null, Func<IScreenContext, ScreenResult>? onBack = null,
        Action<ReadingPosition>? positionChanged = null, string? entryPrefix = null)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _onMenu = onMenu;
        _onBack = onBack;
        _positionChanged = positionChanged;
        _entryPrefix = entryPrefix;

        var lines = document.Paragraphs.Select(p => LineOf(p.Text)).ToArray();
        _paragraphStarts = new int[Math.Max(1, lines.Length)];
        for (var i = 1; i < lines.Length; i++) _paragraphStarts[i] = _paragraphStarts[i - 1] + lines[i - 1].Length + 1;

        var buffer = new TextBuffer(string.Join('\n', lines));
        _navigator = new TextNavigator(buffer, LineDescription);
        _reading = new ContinuousReader(MoveTo, Private);

        start ??= ReadingPosition.Beginning;
        var paragraph = Math.Clamp(start.Paragraph, 0, _paragraphStarts.Length - 1);
        var offset = Math.Clamp(start.Offset, 0, ParagraphEnd(paragraph) - _paragraphStarts[paragraph]);
        buffer.MoveTo(_paragraphStarts[paragraph] + offset);
        _reported = CurrentPosition;
        ChapterCount = Math.Max(document.ChapterTitles.Count, document.Paragraphs.Count == 0 ? 0 : document.Paragraphs.Max(p => p.Chapter));
    }

    public string Title { get; }

    public KeyProfile KeyProfile => KeyProfile.TextReading;

    /// <summary>The cursor's place in the document.</summary>
    public ReadingPosition CurrentPosition
    {
        get
        {
            var paragraph = CurrentParagraphIndex;
            return new ReadingPosition(paragraph, Buffer.Cursor - _paragraphStarts[paragraph]);
        }
    }

    public bool IsReadingAll => _reading.IsReading;

    private TextBuffer Buffer => _navigator.Buffer;

    private int CurrentParagraphIndex => ParagraphAt(Buffer.Cursor);

    private DocumentParagraph? CurrentParagraph =>
        CurrentParagraphIndex < _document.Paragraphs.Count ? _document.Paragraphs[CurrentParagraphIndex] : null;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var first = !_entered;
        _entered = true;
        var line = _navigator.Line(context);
        return Private(first && !string.IsNullOrEmpty(_entryPrefix)
            ? context.Localizer.Get("reader.enter.resume", ("title", Title), ("prefix", _entryPrefix), ("line", line))
            : context.Localizer.Get("reader.enter", ("title", Title), ("line", line)));
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        // Any key stops reading aloud (the shell has already silenced the voice). Only Escape and the menu then do
        // what they always do; any other key just stops, so the cursor stays on the interrupted sentence.
        if (_reading.Stop() && input.Command is not (Command.Back or Command.ScreenMenu)) return ScreenResult.Handled;

        if (input.Character is null && _navigator.Move(input.Command, context) is var (said, earcon))
        {
            Moved();
            return Say(said, earcon);
        }

        return input.Character is not null ? ScreenResult.Unhandled : input.Command switch
        {
            Command.ReadAll or Command.Activate => ReadAll(context),
            Command.NextHeading => Heading(1, context),
            Command.PreviousHeading => Heading(-1, context),
            Command.NextPage => Page(1, context),
            Command.PreviousPage => Page(-1, context),
            Command.ScreenMenu => _onMenu?.Invoke(context) ?? ScreenResult.Unhandled,
            Command.Back => _onBack?.Invoke(context) ?? ScreenResult.Pop(),
            _ => ScreenResult.Unhandled,
        };
    }

    public Utterance Help(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Private(context.Localizer.Get("reader.help"));
    }

    public ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context) =>
        _reading.SpeechFinished(token, outcome, context);

    /// <summary>
    /// Reads everything from the start of the sentence that contains the cursor to the end of the document (Enter, or
    /// a menu). Crossing into a new page or chapter, its first sentence starts with "Pagina 4." / "Capitolul 2."
    /// </summary>
    public ScreenResult ReadAll(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _reading.Start(WithBoundaries(Sentences.From(Buffer.Text, Buffer.Cursor), context), context);
    }

    /// <summary>"Rândul 3 din 40." and, for a PDF, "Pagina 2 din 5." or, for an EPUB, "Capitolul 1 din 12."</summary>
    public Utterance Position(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var l = context.Localizer;
        var said = l.Get("reader.position", ("line", CurrentParagraphIndex + 1), ("lines", _paragraphStarts.Length));
        if (_document.Format == DocumentFormat.Pdf && _document.PageCount > 0)
        {
            said += " " + l.Get("reader.position.page", ("page", CurrentPage), ("pages", _document.PageCount));
        }
        else if (_document.Format == DocumentFormat.Epub && ChapterCount > 0)
        {
            said += " " + l.Get("reader.position.chapter", ("chapter", Math.Max(1, CurrentParagraph?.Chapter ?? 1)), ("chapters", ChapterCount));
        }
        return Private(said);
    }

    private int CurrentPage => _pageWithoutText ?? Math.Max(1, CurrentParagraph?.Page ?? 1);

    /// <summary>Chapters of an EPUB: its titles, or the paragraphs' highest chapter when titles are missing.</summary>
    private int ChapterCount { get; }

    private IEnumerable<ReadingSentence> WithBoundaries(IEnumerable<ReadingSentence> sentences, IScreenContext context)
    {
        var format = _document.Format;
        int? previous = null;
        foreach (var sentence in sentences)
        {
            var paragraph = ParagraphAt(sentence.Start);
            var part = paragraph < _document.Paragraphs.Count
                ? format switch
                {
                    DocumentFormat.Pdf => _document.Paragraphs[paragraph].Page,
                    DocumentFormat.Epub => _document.Paragraphs[paragraph].Chapter,
                    _ => 0,
                }
                : 0;
            if (previous is { } before && part != before && part > 0)
            {
                var text = format == DocumentFormat.Pdf
                    ? context.Localizer.Get("reader.readAll.page", ("page", part), ("text", sentence.Text))
                    : context.Localizer.Get("reader.readAll.chapter", ("chapter", part), ("text", sentence.Text));
                yield return sentence with { Text = text };
            }
            else
            {
                yield return sentence;
            }
            previous = part;
        }
    }

    private ScreenResult Heading(int direction, IScreenContext context)
    {
        var paragraphs = _document.Paragraphs;
        for (var i = CurrentParagraphIndex + direction; i >= 0 && i < paragraphs.Count; i += direction)
        {
            if (paragraphs[i].HeadingLevel <= 0) continue;
            GoToParagraph(i);
            return Say(HeadingText(paragraphs[i], context));
        }
        return Say(context.Localizer.Get(direction > 0 ? "reader.noHeadingAfter" : "reader.noHeadingBefore"), Earcon.EdgeOfList);
    }

    private ScreenResult Page(int direction, IScreenContext context) => _document.Format switch
    {
        DocumentFormat.Pdf when _document.PageCount > 0 => PdfPage(direction, context),
        DocumentFormat.Epub when ChapterCount > 0 => Chapter(direction, context),
        _ => Heading(direction, context),
    };

    private ScreenResult PdfPage(int direction, IScreenContext context)
    {
        var l = context.Localizer;
        var pages = _document.PageCount;
        var target = CurrentPage + direction;
        if (target < 1) return Say(l.Get("reader.page.first"), Earcon.EdgeOfList);
        if (target > pages) return Say(l.Get("reader.page.last"), Earcon.EdgeOfList);

        var first = FindParagraph(p => p.Page == target);
        var scanned = _document.ScannedPages.Contains(target);
        if (first >= 0)
        {
            GoToParagraph(first);
            return Say(scanned
                ? l.Get("reader.page.scanned", ("page", target))
                : l.Get("reader.page", ("page", target), ("pages", pages), ("line", _navigator.Line(context))));
        }

        // No text on that page: the cursor waits on the text that follows it.
        var after = FindParagraph(p => p.Page > target);
        if (after >= 0) GoToParagraph(after);
        _pageWithoutText = target;
        return Say(scanned
            ? l.Get("reader.page.scanned", ("page", target))
            : l.Get("reader.page.empty", ("page", target), ("pages", pages)));
    }

    private ScreenResult Chapter(int direction, IScreenContext context)
    {
        var l = context.Localizer;
        var target = Math.Max(1, CurrentParagraph?.Chapter ?? 1) + direction;
        if (target < 1) return Say(l.Get("reader.chapter.first"), Earcon.EdgeOfList);
        var first = target > ChapterCount ? -1 : FindParagraph(p => p.Chapter >= target);
        if (first < 0) return Say(l.Get("reader.chapter.last"), Earcon.EdgeOfList);

        var chapter = _document.Paragraphs[first].Chapter;
        GoToParagraph(first);
        var title = chapter <= _document.ChapterTitles.Count ? _document.ChapterTitles[chapter - 1] : "";
        var line = _navigator.Line(context);
        return Say(string.IsNullOrWhiteSpace(title)
            ? l.Get("reader.chapter.untitled", ("chapter", chapter), ("line", line))
            : l.Get("reader.chapter", ("chapter", chapter), ("title", title), ("line", line)));
    }

    /// <summary>The current line; a heading says its level: "Titlu 2: Rețete".</summary>
    private string LineDescription(IScreenContext context) =>
        CurrentParagraph is { HeadingLevel: > 0 } heading && heading.Text.Trim().Length > 0
            ? HeadingText(heading, context)
            : _navigator.PlainLine(context);

    private string HeadingText(DocumentParagraph heading, IScreenContext context) =>
        context.Localizer.Get("reader.heading", ("level", heading.HeadingLevel), ("text", LineOf(heading.Text)));

    private int FindParagraph(Func<DocumentParagraph, bool> match)
    {
        var paragraphs = _document.Paragraphs;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (match(paragraphs[i])) return i;
        }
        return -1;
    }

    private void GoToParagraph(int paragraph)
    {
        Buffer.MoveTo(_paragraphStarts[paragraph]);
        Moved();
    }

    /// <summary>Reading aloud moved the cursor.</summary>
    private void MoveTo(int position)
    {
        Buffer.MoveTo(position);
        Moved();
    }

    private void Moved()
    {
        _pageWithoutText = null;
        var position = CurrentPosition;
        if (position == _reported) return;
        _reported = position;
        _positionChanged?.Invoke(position);
    }

    /// <summary>The paragraph that contains <paramref name="position"/> of the buffer's text.</summary>
    private int ParagraphAt(int position)
    {
        var index = Array.BinarySearch(_paragraphStarts, position);
        return index >= 0 ? index : ~index - 1;
    }

    private int ParagraphEnd(int paragraph) =>
        paragraph + 1 < _paragraphStarts.Length ? _paragraphStarts[paragraph + 1] - 1 : Buffer.Length;

    private ScreenResult Say(string text, Earcon earcon = Earcon.None) => ScreenResult.Say(Private(new Utterance(text, Earcon: earcon)));

    /// <summary>Masked in the diagnostic log; the window shows the whole document.</summary>
    private Utterance Private(Utterance utterance) => utterance with { DisplayText = Buffer.Text, Sensitive = true };

    /// <summary>A paragraph is one line: line breaks inside it become spaces (same length, so offsets stay).</summary>
    private static string LineOf(string paragraph) => paragraph.Replace('\r', ' ').Replace('\n', ' ');
}
