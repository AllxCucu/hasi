namespace Hasi.Core.Input;

/// <summary>
/// Maps key chords to commands. Every global command has a binding that works without F keys,
/// because many laptops need Fn for F1-F12 and have no numeric keypad.
/// A <see cref="KeyProfile"/> adds a second layer of bindings that wins over the default ones on the screens
/// that ask for it.
/// </summary>
public sealed class KeyMap
{
    private readonly Dictionary<KeyChord, Command> _bindings = [];
    private readonly Dictionary<KeyProfile, Dictionary<KeyChord, Command>> _profiles = [];

    /// <summary>The default bindings, used on every screen.</summary>
    public IReadOnlyDictionary<KeyChord, Command> Bindings => _bindings;

    public KeyMap Bind(KeyChord chord, Command command)
    {
        _bindings[chord] = command;
        return this;
    }

    /// <summary>Binds <paramref name="chord"/> only on screens with <paramref name="profile"/>.</summary>
    public KeyMap Bind(KeyChord chord, Command command, KeyProfile profile)
    {
        if (profile == KeyProfile.Default) return Bind(chord, command);
        if (!_profiles.TryGetValue(profile, out var layer)) _profiles[profile] = layer = [];
        layer[chord] = command;
        return this;
    }

    public bool Unbind(KeyChord chord) => _bindings.Remove(chord);

    /// <summary>Removes a binding of <paramref name="profile"/>; the chord then falls back to its default command.</summary>
    public bool Unbind(KeyChord chord, KeyProfile profile) =>
        profile == KeyProfile.Default ? Unbind(chord) : _profiles.TryGetValue(profile, out var layer) && layer.Remove(chord);

    /// <summary>The bindings <paramref name="profile"/> adds on top of the default ones.</summary>
    public IReadOnlyDictionary<KeyChord, Command> ProfileBindings(KeyProfile profile) =>
        profile == KeyProfile.Default ? _bindings
        : _profiles.TryGetValue(profile, out var layer) ? layer
        : new Dictionary<KeyChord, Command>();

    /// <summary>
    /// The command of <paramref name="chord"/> on a screen with <paramref name="profile"/>: the profile's own binding
    /// first, then the default one.
    /// </summary>
    public Command Resolve(KeyChord chord, KeyProfile profile) =>
        profile != KeyProfile.Default && _profiles.TryGetValue(profile, out var layer) && layer.TryGetValue(chord, out var command)
            ? command
            : Resolve(chord);

    public Command Resolve(KeyChord chord) =>
        _bindings.TryGetValue(chord, out var command) ? command : Command.None;

    public IEnumerable<KeyChord> ChordsFor(Command command) =>
        _bindings.Where(b => b.Value == command).Select(b => b.Key);

    public static KeyMap CreateDefault()
    {
        const KeyModifiers ctrl = KeyModifiers.Ctrl;
        const KeyProfile editing = KeyProfile.TextEditing;
        const KeyProfile reading = KeyProfile.TextReading;
        return new KeyMap()
            // Navigation
            .Bind(Key.Up, Command.Previous).Bind(Key.NumPad8, Command.Previous)
            .Bind(Key.Down, Command.Next).Bind(Key.NumPad2, Command.Next)
            .Bind(Key.Home, Command.First).Bind(new(Key.Up, ctrl), Command.First).Bind(Key.NumPad7, Command.First)
            .Bind(Key.End, Command.Last).Bind(new(Key.Down, ctrl), Command.Last).Bind(Key.NumPad1, Command.Last)
            .Bind(Key.Enter, Command.Activate).Bind(Key.NumPad5, Command.Activate)
            // Num Lock off: the keypad 5 arrives as Clear. Insert (keypad 0) stays unbound: it is also
            // the main-keyboard Insert and the NVDA modifier.
            .Bind(Key.Clear, Command.Activate)
            .Bind(Key.Escape, Command.Back).Bind(Key.NumPad0, Command.Back)
            .Bind(Key.Left, Command.Decrease).Bind(Key.NumPad4, Command.Decrease)
            .Bind(Key.Right, Command.Increase).Bind(Key.NumPad6, Command.Increase)
            .Bind(Key.PageDown, Command.NextPage).Bind(Key.NumPad3, Command.NextPage)
            .Bind(Key.PageUp, Command.PreviousPage).Bind(Key.NumPad9, Command.PreviousPage)
            .Bind(Key.Backspace, Command.DeletePrevious)
            // Global
            .Bind(Key.F1, Command.Help).Bind(new(Key.H, ctrl), Command.Help).Bind(Key.Divide, Command.Help)
            .Bind(Key.F2, Command.RepeatLast).Bind(new(Key.R, ctrl), Command.RepeatLast).Bind(Key.Multiply, Command.RepeatLast)
            .Bind(Key.F3, Command.WhereAmI).Bind(new(Key.U, ctrl), Command.WhereAmI).Bind(Key.Subtract, Command.WhereAmI)
            .Bind(new(Key.T, ctrl), Command.SayTime)
            .Bind(Key.LeftCtrl, Command.StopSpeech).Bind(Key.RightCtrl, Command.StopSpeech)
            .Bind(new(Key.F4, KeyModifiers.Alt), Command.Exit)
            .Bind(new(Key.F12, ctrl | KeyModifiers.Shift), Command.ToggleDiagnostic)
            .Bind(new(Key.D, ctrl | KeyModifiers.Shift), Command.ToggleDiagnostic)
            // Text editing. Up/Down (previous/next line), Backspace, Escape and the global keys keep their default
            // commands. With Num Lock off the keypad arrives as the arrows, Home and End, so it follows these too.
            .Bind(Key.Left, Command.PreviousCharacter, editing).Bind(Key.Right, Command.NextCharacter, editing)
            .Bind(new(Key.Left, ctrl), Command.PreviousWord, editing).Bind(new(Key.Right, ctrl), Command.NextWord, editing)
            .Bind(Key.Home, Command.LineStart, editing).Bind(Key.End, Command.LineEnd, editing)
            .Bind(new(Key.Home, ctrl), Command.DocumentStart, editing).Bind(new(Key.Up, ctrl), Command.DocumentStart, editing)
            .Bind(new(Key.End, ctrl), Command.DocumentEnd, editing).Bind(new(Key.Down, ctrl), Command.DocumentEnd, editing)
            .Bind(Key.Enter, Command.NewLine, editing)
            .Bind(Key.Delete, Command.DeleteNext, editing)
            .Bind(new(Key.S, ctrl), Command.Save, editing)
            .Bind(new(Key.Z, ctrl), Command.Undo, editing).Bind(new(Key.Y, ctrl), Command.Redo, editing)
            .Bind(Key.F10, Command.ScreenMenu, editing).Bind(new(Key.M, ctrl), Command.ScreenMenu, editing)
            // Reading a document: the same moves, no typing. Page Down / Page Up (and the keypad's 3 / 9) keep their
            // default NextPage / PreviousPage. The reader is no text field, so the keypad always moves: 4 / 6 the
            // character, 7 / 1 the start / end of the line (like Home / End), 5 reads all (like Enter).
            .Bind(Key.Left, Command.PreviousCharacter, reading).Bind(Key.Right, Command.NextCharacter, reading)
            .Bind(Key.NumPad4, Command.PreviousCharacter, reading).Bind(Key.NumPad6, Command.NextCharacter, reading)
            .Bind(new(Key.Left, ctrl), Command.PreviousWord, reading).Bind(new(Key.Right, ctrl), Command.NextWord, reading)
            .Bind(Key.Home, Command.LineStart, reading).Bind(Key.End, Command.LineEnd, reading)
            .Bind(Key.NumPad7, Command.LineStart, reading).Bind(Key.NumPad1, Command.LineEnd, reading)
            .Bind(new(Key.Home, ctrl), Command.DocumentStart, reading).Bind(new(Key.Up, ctrl), Command.DocumentStart, reading)
            .Bind(new(Key.End, ctrl), Command.DocumentEnd, reading).Bind(new(Key.Down, ctrl), Command.DocumentEnd, reading)
            .Bind(Key.Enter, Command.ReadAll, reading).Bind(Key.NumPad5, Command.ReadAll, reading).Bind(Key.Clear, Command.ReadAll, reading)
            .Bind(Key.H, Command.NextHeading, reading).Bind(new(Key.H, KeyModifiers.Shift), Command.PreviousHeading, reading)
            .Bind(Key.F10, Command.ScreenMenu, reading).Bind(new(Key.M, ctrl), Command.ScreenMenu, reading);
    }
}
