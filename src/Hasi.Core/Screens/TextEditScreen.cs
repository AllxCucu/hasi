using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Text;

namespace Hasi.Core.Screens;

/// <summary>
/// A multi-line text editor. Typing follows the key echo setting like <see cref="TextInputScreen"/>; the arrows read
/// the character, the word (with Control) or the line; Backspace and Delete say what they deleted. Saving, the
/// editor's menu and leaving belong to whoever opened the screen, through the callbacks.
/// Everything it says is private content: masked in the diagnostic log, with the whole text shown on screen.
/// </summary>
/// <param name="onMenu">F10 / Ctrl+M; null: the key is unavailable.</param>
/// <param name="onSave">Ctrl+S; null: the key is unavailable.</param>
/// <param name="onBack">Escape; null: the screen closes.</param>
public sealed class TextEditScreen(
    string title,
    TextBuffer buffer,
    Func<IScreenContext, ScreenResult>? onMenu = null,
    Func<IScreenContext, ScreenResult>? onSave = null,
    Func<IScreenContext, ScreenResult>? onBack = null) : IScreen, ITextEntry, IHasKeyProfile, ISensitiveScreen
{
    private readonly TextNavigator _navigator = new(buffer ?? throw new ArgumentNullException(nameof(buffer)));

    /// <summary>The document's name; it changes when a new document is saved under a name.</summary>
    public string Title { get; set; } = title;

    public TextBuffer Buffer => _navigator.Buffer;

    public KeyProfile KeyProfile => KeyProfile.TextEditing;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        Private(context.Localizer.Get("edit.enter", ("title", Title), ("line", _navigator.Line(context))));

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);
        if (input.Character is { } c && !char.IsControl(c)) return Typed(c, context);
        if (_navigator.Move(input.Command, context) is var (said, earcon)) return Say(said, earcon);

        var l = context.Localizer;
        return input.Command switch
        {
            Command.NewLine => NewLine(context),
            Command.DeletePrevious => Buffer.DeletePrevious() is { } deleted ? Deleted(deleted, context) : Say(_navigator.StartEdge(context)),
            Command.DeleteNext => Buffer.DeleteNext() is { } deleted ? Deleted(deleted, context) : Say(_navigator.EndEdge(context)),
            Command.Undo => Buffer.Undo()
                ? Say(l.Get("edit.undone", ("line", _navigator.Line(context))))
                : Say(l.Get("edit.nothingToUndo")),
            Command.Redo => Buffer.Redo()
                ? Say(l.Get("edit.redone", ("line", _navigator.Line(context))))
                : Say(l.Get("edit.nothingToRedo")),
            Command.Save => onSave?.Invoke(context) ?? ScreenResult.Unhandled,
            Command.ScreenMenu => onMenu?.Invoke(context) ?? ScreenResult.Unhandled,
            Command.Back => onBack?.Invoke(context) ?? ScreenResult.Pop(),
            _ => ScreenResult.Unhandled,
        };
    }

    public Utterance Help(IScreenContext context) => Private(context.Localizer.Get("edit.help"));

    /// <summary>"Rândul 3 din 10, coloana 5.", for the editor's menu.</summary>
    public Utterance Position(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Private(context.Localizer.Get("edit.position",
            ("line", Buffer.LineNumber), ("lines", Buffer.LineCount), ("column", Buffer.ColumnNumber)));
    }

    private ScreenResult Typed(char c, IScreenContext context)
    {
        var before = Buffer.Length;
        Buffer.Insert(c);
        // Half of a surrogate pair: nothing is inserted (or said) until the other half arrives.
        if (Buffer.Length == before) return Say("");
        var typed = Buffer.Text[(Buffer.Cursor - (Buffer.Length - before))..Buffer.Cursor];
        return context.Options.KeyEcho switch
        {
            Settings.KeyEcho.None => Say(""),
            // Word echo: letters are silent; a space or punctuation says the word just finished.
            Settings.KeyEcho.Word when TextBuffer.IsWordCharacter(typed[0]) => Say(""),
            Settings.KeyEcho.Word => Say(Buffer.WordEndingAt(Buffer.Cursor - typed.Length) is { Length: > 0 } word
                ? word
                : TextNavigator.ElementName(typed, context)),
            _ => Say(TextNavigator.ElementName(typed, context)),
        };
    }

    private ScreenResult NewLine(IScreenContext context)
    {
        Buffer.InsertNewLine();
        return Say(context.Options.KeyEcho == Settings.KeyEcho.None ? "" : context.Localizer.Get("edit.newLine"));
    }

    private ScreenResult Deleted(string deleted, IScreenContext context)
    {
        var name = deleted == "\n" ? context.Localizer.Get("edit.lineBreakName") : TextNavigator.ElementName(deleted, context);
        return Say(context.Localizer.Get("textInput.deleted", ("char", name)));
    }

    private ScreenResult Say((string Text, Earcon Earcon) said) => Say(said.Text, said.Earcon);

    private ScreenResult Say(string text, Earcon earcon = Earcon.None) => ScreenResult.Say(Private(text, earcon));

    /// <summary>Masked in the diagnostic log; the window shows the whole document.</summary>
    private Utterance Private(string text, Earcon earcon = Earcon.None) =>
        new(text, Earcon: earcon, DisplayText: Buffer.Text, Sensitive: true);
}
