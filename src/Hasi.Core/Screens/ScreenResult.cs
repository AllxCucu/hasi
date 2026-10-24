using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>What a screen asks the shell to do after handling a command.</summary>
public abstract record ScreenResult
{
    /// <summary>Done, nothing to say.</summary>
    public static ScreenResult Handled { get; } = new HandledResult();

    /// <summary>The screen does not use this command; the shell decides what happens.</summary>
    public static ScreenResult Unhandled { get; } = new UnhandledResult();

    public static ScreenResult Say(Utterance utterance) => new SayResult(utterance);

    public static ScreenResult Push(IScreen screen) => new PushResult(screen);

    public static ScreenResult Replace(IScreen screen) => new ReplaceResult(screen);

    /// <summary>Closes the current screen.</summary>
    /// <param name="prefix">Said before the screen below announces itself, for example "Anulat."</param>
    /// <param name="announce">Whether the screen below announces itself.</param>
    /// <param name="then">Applied after closing, instead of the announcement.</param>
    public static ScreenResult Pop(Utterance? prefix = null, bool announce = true, ScreenResult? then = null) =>
        new PopResult(prefix, announce, then);

    /// <summary>Asks a yes/no question; <paramref name="onYes"/> runs only after Enter.</summary>
    /// <param name="cancelled">Said after Escape instead of "Anulat."</param>
    public static ScreenResult Confirm(string question, Func<ScreenResult> onYes, Utterance? cancelled = null) =>
        new ConfirmResult(question, onYes, cancelled);
}

public sealed record HandledResult : ScreenResult;

public sealed record UnhandledResult : ScreenResult;

public sealed record SayResult(Utterance Utterance) : ScreenResult;

public sealed record PushResult(IScreen Screen) : ScreenResult;

public sealed record ReplaceResult(IScreen Screen) : ScreenResult;

public sealed record PopResult(Utterance? Prefix, bool Announce, ScreenResult? Then) : ScreenResult;

public sealed record ConfirmResult(string Question, Func<ScreenResult> OnYes, Utterance? Cancelled = null) : ScreenResult;

/// <summary>
/// Closes <paramref name="Screen"/> and every screen above it without announcing anything, then applies what
/// <paramref name="Then"/> returns at that moment. Nothing happens when the screen is no longer open.
/// </summary>
internal sealed record CloseResult(IScreen Screen, Func<ScreenResult> Then) : ScreenResult;
