using System.Text;
using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// A one-line text field. Every typed character is spoken; Backspace deletes and says what it deleted;
/// the up and down arrows read the whole text; Enter hands the text to <paramref name="submit"/>.
/// The text stays after Enter so it can be corrected and sent again.
/// </summary>
/// <param name="defaultValue">Submitted by Enter on an empty field, when not null (a proposed document name).</param>
public sealed class TextInputScreen(
    string title,
    string prompt,
    Func<string, IScreenContext, ScreenResult> submit,
    string? defaultValue = null) : IScreen, ITextEntry
{
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
            return context.Options.KeyEcho switch
            {
                Settings.KeyEcho.None => Say(""),
                // Word echo: letters are silent; a space or punctuation says the word just finished.
                Settings.KeyEcho.Word when char.IsLetterOrDigit(c) => Say(""),
                Settings.KeyEcho.Word => Say(LastWord() is { Length: > 0 } word ? word : CharacterName(c, context)),
                _ => Say(CharacterName(c, context)),
            };
        }

        return input.Command switch
        {
            Command.DeletePrevious when _text.Length == 0 =>
                ScreenResult.Say(new Utterance(l.Get("textInput.empty"), Earcon: Earcon.EdgeOfList)),
            Command.DeletePrevious => DeleteLast(context),
            Command.Next or Command.Previous => _text.Length == 0
                ? ScreenResult.Say(l.Get("textInput.empty"))
                : Say(l.Get("textInput.readBack", ("text", Text))),
            Command.Activate when _text.Length > 0 => submit(Text, context),
            Command.Activate when defaultValue is not null => submit(defaultValue, context),
            Command.Activate => ScreenResult.Say(l.Get("textInput.emptySubmit")),
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

    /// <summary>The word that ends just before the separator typed last.</summary>
    private string LastWord()
    {
        var end = _text.Length - 1; // the separator
        var start = end;
        while (start > 0 && char.IsLetterOrDigit(_text[start - 1])) start--;
        return _text.ToString(start, end - start);
    }

    /// <summary>Speaks <paramref name="text"/> while the screen shows what has been typed so far.</summary>
    private ScreenResult Say(string text) => ScreenResult.Say(new Utterance(text, DisplayText: Text));

    private static string CharacterName(char c, IScreenContext context) => KeyNames.Character(c, context.Localizer);
}
