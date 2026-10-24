namespace Hasi.Core.Input;

/// <summary>Decides whether a key press types a character or is a HASI command.</summary>
public static class TextRouting
{
    /// <summary>
    /// True when the key should produce a typed character instead of a command.
    /// In a text field with Num Lock on, the numeric keypad types digits; everywhere else (and with
    /// Num Lock off) it keeps its approved navigation bindings. Its operator keys (/ * -) stay commands.
    /// </summary>
    /// <param name="profile">The current screen's key profile (<c>Shell.ActiveKeyProfile</c>).</param>
    public static bool ProducesText(KeyChord chord, KeyMap keyMap, bool textEntryActive, bool numLockOn,
        KeyProfile profile = KeyProfile.Default)
    {
        ArgumentNullException.ThrowIfNull(keyMap);
        if (textEntryActive && numLockOn && chord.Modifiers == KeyModifiers.None
            && (chord.Key.IsNumPadDigit() || chord.Key == Key.Decimal))
        {
            return true;
        }
        return keyMap.Resolve(chord, profile) == Command.None && MayTypeCharacter(chord);
    }

    /// <summary>
    /// Letters, digits, space and punctuation, alone, with Shift, or with AltGr (Ctrl+Alt) as used by
    /// some Romanian layouts for ș and ț.
    /// </summary>
    public static bool MayTypeCharacter(KeyChord chord)
    {
        var plainOrShift = (chord.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None;
        var altGr = chord.Modifiers.HasFlag(KeyModifiers.Ctrl) && chord.Modifiers.HasFlag(KeyModifiers.Alt);
        return (plainOrShift || altGr) && chord.Key is
            (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or Key.Space or Key.Other;
    }
}
