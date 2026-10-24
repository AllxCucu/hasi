using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// Spoken yes/no question for destructive actions. Enter = yes, Escape = no. Any other key repeats
/// the question, so a confused user is never left in silence.
/// </summary>
/// <param name="cancelled">Said after Escape; "Anulat." when null.</param>
public sealed class ConfirmScreen(string question, Func<ScreenResult> onYes, string title, Utterance? cancelled = null) : IScreen
{
    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("confirm.prompt", ("question", question));

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.NextPage or Command.PreviousPage => ScreenResult.Unhandled,
        Command.Activate => ScreenResult.Pop(announce: false, then: onYes()),
        Command.Back => ScreenResult.Pop(prefix: cancelled ?? context.Localizer.Get("confirm.cancelled")),
        _ => ScreenResult.Say(Enter(EnterReason.Initial, context)),
    };

    public Utterance Help(IScreenContext context) => context.Localizer.Get("confirm.help");
}
