using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>A screen that only says something. Enter or the arrows say it again; Escape goes back.</summary>
public sealed class MessageScreen(string title, string message) : IScreen
{
    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("message.enter", ("title", Title), ("message", message));

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Activate or Command.Next or Command.Previous or Command.First or Command.Last =>
            ScreenResult.Say(message),
        _ => ScreenResult.Unhandled,
    };

    public Utterance Help(IScreenContext context) =>
        context.Localizer.Get("message.help", ("title", Title));
}
