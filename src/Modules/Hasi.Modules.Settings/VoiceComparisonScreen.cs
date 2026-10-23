using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Speech;

namespace Hasi.Modules.Settings;

/// <summary>
/// Diagnostic → Comparare voci: a fixed list of test phrases. Enter plays the selected phrase through every
/// variant in turn (for example Paul through SAPI, then Paul through RHVoice directly); the left and right
/// arrows play only the first or only the second variant. Any key stops the comparison.
/// </summary>
internal sealed class VoiceComparisonScreen(
    string title,
    IReadOnlyList<(string Label, string Text)> phrases,
    IVoiceComparison comparison,
    string help)
    : MenuScreen(title, [.. phrases.Select(p => new MenuItem(p.Label))], help)
{
    public override ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        var text = phrases[SelectedIndex].Text;
        switch (input.Command)
        {
            case Command.Activate:
                comparison.Play(text);
                return ScreenResult.Handled;
            case Command.Decrease:
                comparison.Play(text, 0);
                return ScreenResult.Handled;
            case Command.Increase when comparison.Variants.Count > 1:
                comparison.Play(text, 1);
                return ScreenResult.Handled;
            default:
                return base.Handle(input, context);
        }
    }

    /// <summary>The screen shown when one of the voices to compare is missing.</summary>
    public static MessageScreen Unavailable(string title, string message) => new(title, message);
}
