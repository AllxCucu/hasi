using System.Globalization;

using Hasi.Core.Documents;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Documents;

/// <summary>
/// One document being read: the core <see cref="TextReaderScreen"/> plus the document's menu (read all, position, go to
/// a page of a PDF, close) and keeping the reading position among the recent documents: at most once every
/// <see cref="SaveInterval"/> while it changes, and when the document is closed. Everything in it is private.
/// </summary>
public sealed class DocumentReaderScreen : IScreen, IHasKeyProfile, ISensitiveScreen, ISpeechListener, ITickingScreen
{
    /// <summary>The least time between two saves of the reading position.</summary>
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(5);

    private readonly DocumentsServices _services;
    private readonly Document _document;
    private readonly TextReaderScreen _reader;

    private bool _entered;

    /// <summary>The position changed since it was last saved.</summary>
    private bool _changed;

    private DateTimeOffset _savedAt;

    /// <param name="notices">Said once, after the title, before the first line (scanned pages, broken text, resuming).</param>
    internal DocumentReaderScreen(DocumentsServices services, string path, string title, Document document, ReadingPosition? start,
        string? notices)
    {
        _services = services;
        _document = document;
        FilePath = path;
        _reader = new TextReaderScreen(title, document, start, OpenMenu, Leave, _ => _changed = true, notices);
        _savedAt = services.Context.Time.GetUtcNow();
    }

    public string Title => _reader.Title;

    public string FilePath { get; }

    public TextReaderScreen Reader => _reader;

    public KeyProfile KeyProfile => _reader.KeyProfile;

    /// <summary>This document as the recent list keeps it, read at <paramref name="now"/>.</summary>
    internal RecentReading Reading(DateTimeOffset now)
    {
        var position = _reader.CurrentPosition;
        var page = _document.Format == DocumentFormat.Pdf && position.Paragraph < _document.Paragraphs.Count
            ? _document.Paragraphs[position.Paragraph].Page
            : 0;
        return new RecentReading(FilePath, Title, _document.Format, position, page, now);
    }

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        var entry = _reader.Enter(reason, context);
        // Text that looks broken is announced once, with the error sound, before the first line.
        if (!_entered && _document.LooksGarbled) entry = entry with { Earcon = Earcon.Error };
        _entered = true;
        return entry;
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _reader.Handle(input, context);

    public Utterance Help(IScreenContext context) => _reader.Help(context);

    public ScreenResult SpeechFinished(long token, SpeechOutcome outcome, IScreenContext context) =>
        _reader.SpeechFinished(token, outcome, context);

    public void Tick(DateTimeOffset now)
    {
        if (_changed && now - _savedAt >= SaveInterval) SavePosition(now);
    }

    /// <summary>A failed write keeps the change, to be tried again at the next tick or on leaving.</summary>
    private void SavePosition(DateTimeOffset now)
    {
        if (!_services.SaveRecent(Reading(now))) return;
        _changed = false;
        _savedAt = now;
    }

    /// <summary>Escape or "Închide documentul": the position is kept, then the document closes (it must be the current screen).</summary>
    private ScreenResult Leave(IScreenContext context)
    {
        if (_changed) SavePosition(_services.Context.Time.GetUtcNow());
        return ScreenResult.Pop();
    }

    private ScreenResult OpenMenu(IScreenContext context)
    {
        var l = context.Localizer;
        List<MenuItem> items =
        [
            new(l.Get("documents.menu.readAll"), c => ScreenResult.PopTo(this, then: _reader.ReadAll(c))),
            new(l.Get("documents.menu.position"), c => ScreenResult.PopTo(this, then: ScreenResult.Say(_reader.Position(c)))),
        ];
        if (_document.Format == DocumentFormat.Pdf && _document.PageCount > 0) items.Add(new(l.Get("documents.menu.goToPage"), PageField));
        items.Add(new(l.Get("documents.menu.close"), c => ScreenResult.PopTo(this, then: Leave(c))));
        return ScreenResult.Push(new MenuScreen(l.Get("documents.menu.title"), items, l.Get("documents.menu.help")));
    }

    /// <summary>"Mergi la pagina…": the page's number, from 1 to the last page; anything else is asked again.</summary>
    private ScreenResult PageField(IScreenContext context)
    {
        var l = context.Localizer;
        var pages = _document.PageCount;
        return ScreenResult.Push(new TextInputScreen(l.Get("documents.page.title"), l.Get("documents.page.prompt", ("pages", pages)),
            (typed, c) => int.TryParse(typed.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1 && page <= pages
                ? ScreenResult.PopTo(this, then: _reader.GoToPage(page, c))
                : ScreenResult.Say(l.Get("documents.page.invalid", ("pages", pages)))));
    }
}
