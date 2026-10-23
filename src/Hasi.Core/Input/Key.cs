namespace Hasi.Core.Input;

/// <summary>
/// Platform-neutral physical keys. Names match WPF's <c>System.Windows.Input.Key</c> where possible
/// so the host can translate them by name.
/// </summary>
public enum Key
{
    None,

    Up, Down, Left, Right,
    Enter, Escape, Space, Tab, Backspace, Delete, Insert,
    Home, End, PageUp, PageDown,

    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Divide, Multiply, Subtract, Add, Decimal,

    LeftCtrl, RightCtrl, LeftShift, RightShift, LeftAlt, RightAlt, LWin, RWin,

    NumLock, CapsLock, ScrollLock,

    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    /// <summary>Any key HASI does not name individually (punctuation, media keys, ...).</summary>
    Other,
}

public static class KeyExtensions
{
    public static bool IsModifier(this Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    public static bool IsCtrl(this Key key) => key is Key.LeftCtrl or Key.RightCtrl;

    /// <summary>Lock keys: they change a keyboard state and have no command of their own.</summary>
    public static bool IsToggle(this Key key) => key is Key.NumLock or Key.CapsLock or Key.ScrollLock;

    public static bool IsNumPadDigit(this Key key) => key is >= Key.NumPad0 and <= Key.NumPad9;
}
