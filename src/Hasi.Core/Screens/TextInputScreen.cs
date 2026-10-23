using System.Text;
using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// A one-line text field. Every typed character is spoken; Backspace deletes and says what it deleted;
/// the up and down arrows read the whole text; Enter hands the text to <paramref name="submit"/>.
/// The text stays after Enter so it can be corrected and sent again.
/// </summary>
public sealed class TextInputScreen(
    string title,
    string prompt,
    Func<string, IScreenContext, ScreenResult> submit) : IScreen, ITextEntry
{
    private static readonly Dictionary<char, string> CharacterKeys = new()
    {
        [' '] = "char.space",
        [','] = "char.comma",
        ['.'] = "char.period",
        [':'] = "char.colon",
        [';'] = "char.semicolon",
        ['-'] = "char.hyphen",
        ['?'] = "char.question",
        ['!'] = "char.exclamation",
        ['"'] = "char.quote",
        ['\''] = "char.apostrophe",
        ['/'] = "char.slash",
        ['%'] = "char.percent",
        ['+'] = "char.plus",
        ['='] = "char.equals",
        ['('] = "char.parenOpen",
        [')'] = "char.parenClose",
        ['@'] = "char.at",
        ['€'] = "char.euro",
        ['&'] = "char.ampersand",
    };

    private readonly StringBuilder _text = new();

    public string Title { get; } = title;

    public string Text => _text.ToString();

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("textInput.enter", ("title", Title), ("prompt", prompt));

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        var l = context.Localizer;
        if (input.Character is { } c && !char.IsControl(c))
        {
            _text.Append(c);
            return Say(CharacterName(c, context));
        }

        return input.Command switch
        {
            Command.DeletePrevious when _text.Length == 0 =>
                ScreenResult.Say(new Utterance(l.Get("textInput.empty"), Earcon: Earcon.EdgeOfList)),
            Command.DeletePrevious => DeleteLast(context),
            Command.Next or Command.Previous => _text.Length == 0
                ? ScreenResult.Say(l.Get("textInput.empty"))
                : Say(l.Get("textInput.readBack", ("text", Text))),
            Command.Activate => _text.Length == 0
                ? ScreenResult.Say(l.Get("textInput.emptySubmit"))
                : submit(Text, context),
            _ => ScreenResult.Unhandled,
        };
    }

    public Utterance Help(IScreenContext context) => context.Localizer.Get("textInput.help", ("title", Title));

    private ScreenResult DeleteLast(IScreenContext context)
    {
        var deleted = _text[^1];
        _text.Length--;
        return Say(context.Localizer.Get("textInput.deleted", ("char", CharacterName(deleted, context))));
    }

    /// <summary>Speaks <paramref name="text"/> while the screen shows what has been typed so far.</summary>
    private ScreenResult Say(string text) => ScreenResult.Say(new Utterance(text, DisplayText: Text));

    private static string CharacterName(char c, IScreenContext context) =>
        CharacterKeys.TryGetValue(c, out var key) ? context.Localizer.Get(key) : c.ToString();
}
