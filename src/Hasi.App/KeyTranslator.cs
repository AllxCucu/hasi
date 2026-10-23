using System.Windows.Input;
using Hasi.Core.Input;
using HasiKey = Hasi.Core.Input.Key;
using WpfKey = System.Windows.Input.Key;

namespace Hasi.App;

/// <summary>Translates WPF key events into platform-neutral chords.</summary>
internal static class KeyTranslator
{
    public static KeyChord Translate(KeyEventArgs e)
    {
        // With Alt held, WPF reports Key.System and puts the real key in SystemKey.
        var wpfKey = e.Key switch
        {
            WpfKey.System => e.SystemKey,
            WpfKey.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var key = Translate(wpfKey);
        return new KeyChord(key, Modifiers(Keyboard.Modifiers, key));
    }

    internal static HasiKey Translate(WpfKey key) => key switch
    {
        WpfKey.Up => HasiKey.Up,
        WpfKey.Down => HasiKey.Down,
        WpfKey.Left => HasiKey.Left,
        WpfKey.Right => HasiKey.Right,
        WpfKey.Enter => HasiKey.Enter,
        WpfKey.Escape => HasiKey.Escape,
        WpfKey.Space => HasiKey.Space,
        WpfKey.Tab => HasiKey.Tab,
        WpfKey.Back => HasiKey.Backspace,
        WpfKey.Delete => HasiKey.Delete,
        WpfKey.Insert => HasiKey.Insert,
        WpfKey.Home => HasiKey.Home,
        WpfKey.End => HasiKey.End,
        WpfKey.PageUp => HasiKey.PageUp,
        WpfKey.PageDown => HasiKey.PageDown,
        WpfKey.Divide => HasiKey.Divide,
        WpfKey.Multiply => HasiKey.Multiply,
        WpfKey.Subtract => HasiKey.Subtract,
        WpfKey.Add => HasiKey.Add,
        WpfKey.Decimal => HasiKey.Decimal,
        WpfKey.LeftCtrl => HasiKey.LeftCtrl,
        WpfKey.RightCtrl => HasiKey.RightCtrl,
        WpfKey.LeftShift => HasiKey.LeftShift,
        WpfKey.RightShift => HasiKey.RightShift,
        WpfKey.LeftAlt => HasiKey.LeftAlt,
        WpfKey.RightAlt => HasiKey.RightAlt,
        WpfKey.LWin => HasiKey.LWin,
        WpfKey.RWin => HasiKey.RWin,
        >= WpfKey.A and <= WpfKey.Z => HasiKey.A + (key - WpfKey.A),
        >= WpfKey.D0 and <= WpfKey.D9 => HasiKey.D0 + (key - WpfKey.D0),
        >= WpfKey.F1 and <= WpfKey.F12 => HasiKey.F1 + (key - WpfKey.F1),
        >= WpfKey.NumPad0 and <= WpfKey.NumPad9 => HasiKey.NumPad0 + (key - WpfKey.NumPad0),
        _ => HasiKey.Other,
    };

    /// <summary>
    /// Letters, digits, space and punctuation, alone, with Shift, or with AltGr (Ctrl+Alt) as used by
    /// some Romanian layouts for ș and ț.
    /// </summary>
    internal static bool MayTypeCharacter(KeyChord chord)
    {
        var plainOrShift = (chord.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None;
        var altGr = chord.Modifiers.HasFlag(KeyModifiers.Ctrl) && chord.Modifiers.HasFlag(KeyModifiers.Alt);
        return (plainOrShift || altGr) && chord.Key is
            (>= HasiKey.A and <= HasiKey.Z) or (>= HasiKey.D0 and <= HasiKey.D9) or HasiKey.Space or HasiKey.Other;
    }

    /// <summary>Held modifiers, without the modifier being pressed itself (Ctrl alone is plain LeftCtrl).</summary>
    internal static KeyModifiers Modifiers(ModifierKeys held, HasiKey pressed)
    {
        var modifiers = KeyModifiers.None;
        if (held.HasFlag(ModifierKeys.Control) && !pressed.IsCtrl()) modifiers |= KeyModifiers.Ctrl;
        if (held.HasFlag(ModifierKeys.Shift) && pressed is not (HasiKey.LeftShift or HasiKey.RightShift)) modifiers |= KeyModifiers.Shift;
        if (held.HasFlag(ModifierKeys.Alt) && pressed is not (HasiKey.LeftAlt or HasiKey.RightAlt)) modifiers |= KeyModifiers.Alt;
        return modifiers;
    }
}
