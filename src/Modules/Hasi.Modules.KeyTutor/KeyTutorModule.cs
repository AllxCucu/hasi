using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.KeyTutor;

/// <summary>"Învață tastele": press any key to hear what it does, without doing it.</summary>
public sealed class KeyTutorModule : IModule
{
    public string Id => "keyTutor";

    public string TitleKey => "keyTutor.title";

    public IScreen CreateRootScreen(IModuleContext context) => new KeyTutorScreen(context.Localizer.Get(TitleKey));
}

/// <summary>
/// Every key, global ones included (F1, Control, Alt+F4, the numeric keypad), is named and explained instead of
/// being executed. Escape twice in a row leaves, so the user can also learn what Escape does.
/// </summary>
internal sealed class KeyTutorScreen(string title) : IScreen, ICapturesAllKeys
{
    private bool _backPressed;

    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) => context.Localizer.Get("keyTutor.enter");

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        if (input.IsRepeat) return ScreenResult.Handled;
        var l = context.Localizer;

        if (input.Command == Command.Back)
        {
            if (_backPressed) return ScreenResult.Pop();
            _backPressed = true;
            return ScreenResult.Say($"{Describe(input, context)} {l.Get("keyTutor.backAgain")}");
        }
        _backPressed = false;
        return ScreenResult.Say(Describe(input, context));
    }

    public Utterance Help(IScreenContext context) => context.Localizer.Get("keyTutor.enter");

    private static string Describe(InputEvent input, IScreenContext context)
    {
        var l = context.Localizer;
        if (input.Character is { } c)
        {
            return l.Get("keyTutor.character", ("name", KeyNames.CharacterKey(c, l)));
        }

        var name = KeyNames.Chord(input.Key, l);
        var key = input.Key.Key;
        var function = input.Command switch
        {
            Command.None when key.IsToggle() => l.Get($"keyTutor.toggle.{key}"),
            Command.None when key.IsModifier() => l.Get("keyTutor.modifier"),
            _ => l.Get($"command.{input.Command}"),
        };
        return l.Get("keyTutor.describe", ("name", name), ("function", function));
    }
}
