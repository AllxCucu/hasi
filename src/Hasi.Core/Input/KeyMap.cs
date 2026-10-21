namespace Hasi.Core.Input;

/// <summary>
/// Maps key chords to commands. Every global command has a binding that works without F keys,
/// because many laptops need Fn for F1-F12 and have no numeric keypad.
/// </summary>
public sealed class KeyMap
{
    private readonly Dictionary<KeyChord, Command> _bindings = [];

    public IReadOnlyDictionary<KeyChord, Command> Bindings => _bindings;

    public KeyMap Bind(KeyChord chord, Command command)
    {
        _bindings[chord] = command;
        return this;
    }

    public bool Unbind(KeyChord chord) => _bindings.Remove(chord);

    public Command Resolve(KeyChord chord) =>
        _bindings.TryGetValue(chord, out var command) ? command : Command.None;

    public IEnumerable<KeyChord> ChordsFor(Command command) =>
        _bindings.Where(b => b.Value == command).Select(b => b.Key);

    public static KeyMap CreateDefault()
    {
        const KeyModifiers ctrl = KeyModifiers.Ctrl;
        return new KeyMap()
            // Navigation
            .Bind(Key.Up, Command.Previous).Bind(Key.NumPad8, Command.Previous)
            .Bind(Key.Down, Command.Next).Bind(Key.NumPad2, Command.Next)
            .Bind(Key.Home, Command.First).Bind(new(Key.Up, ctrl), Command.First).Bind(Key.NumPad7, Command.First)
            .Bind(Key.End, Command.Last).Bind(new(Key.Down, ctrl), Command.Last).Bind(Key.NumPad1, Command.Last)
            .Bind(Key.Enter, Command.Activate).Bind(Key.NumPad5, Command.Activate)
            .Bind(Key.Escape, Command.Back).Bind(Key.NumPad0, Command.Back)
            .Bind(Key.Left, Command.Decrease).Bind(Key.NumPad4, Command.Decrease)
            .Bind(Key.Right, Command.Increase).Bind(Key.NumPad6, Command.Increase)
            // Global
            .Bind(Key.F1, Command.Help).Bind(new(Key.H, ctrl), Command.Help).Bind(Key.Divide, Command.Help)
            .Bind(Key.F2, Command.RepeatLast).Bind(new(Key.R, ctrl), Command.RepeatLast).Bind(Key.Multiply, Command.RepeatLast)
            .Bind(Key.F3, Command.WhereAmI).Bind(new(Key.U, ctrl), Command.WhereAmI).Bind(Key.Subtract, Command.WhereAmI)
            .Bind(Key.LeftCtrl, Command.StopSpeech).Bind(Key.RightCtrl, Command.StopSpeech)
            .Bind(new(Key.F4, KeyModifiers.Alt), Command.Exit);
    }
}
