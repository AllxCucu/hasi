using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// Spoken yes/no question for destructive actions. Enter = yes, Escape = no. Any other key repeats
/// the question, so a confused user is never left in silence.
/// </summary>
public sealed class ConfirmScreen(string question, Func<ScreenResult> onYes, string title) : IScreen
{
    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("confirm.prompt", ("question", question));

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Activate => ScreenResult.Pop(announce: false, then: onYes()),
        Command.Back => ScreenResult.Pop(prefix: context.Localizer.Get("confirm.cancelled")),
        _ => ScreenResult.Say(Enter(EnterReason.Initial, context)),
    };

    public Utterance Help(IScreenContext context) => context.Localizer.Get("confirm.help");
}
