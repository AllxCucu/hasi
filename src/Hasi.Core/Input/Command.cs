namespace Hasi.Core.Input;

/// <summary>Semantic commands. Screens react to commands, never to physical keys.</summary>
public enum Command
{
    /// <summary>The key has no binding; screens may still use <see cref="InputEvent.Character"/>.</summary>
    None,

    Next,
    Previous,
    First,
    Last,
    Activate,
    Back,
    Increase,
    Decrease,

    // Global commands, handled by the shell on every screen.
    Help,
    RepeatLast,
    WhereAmI,
    StopSpeech,
    Exit,
}

public static class CommandExtensions
{
    public static bool IsGlobal(this Command command) => command is
        Command.Help or Command.RepeatLast or Command.WhereAmI or Command.StopSpeech or Command.Exit;
}

public sealed record InputEvent(Command Command, KeyChord Key, char? Character = null);
