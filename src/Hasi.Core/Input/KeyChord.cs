using System.Text;

namespace Hasi.Core.Input;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>A key plus the modifiers held with it, for example Ctrl+H.</summary>
public readonly record struct KeyChord(Key Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    public static implicit operator KeyChord(Key key) => new(key);

    public override string ToString()
    {
        var text = new StringBuilder();
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) text.Append("Ctrl+");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) text.Append("Shift+");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) text.Append("Alt+");
        return text.Append(Key).ToString();
    }

    /// <summary>Parses the <see cref="ToString"/> form, for example "Ctrl+Shift+D" or "Down".</summary>
    public static KeyChord Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Trim().Split('+');
        var modifiers = KeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            modifiers |= part switch
            {
                "Ctrl" => KeyModifiers.Ctrl,
                "Shift" => KeyModifiers.Shift,
                "Alt" => KeyModifiers.Alt,
                _ => throw new FormatException($"Unknown modifier '{part}' in '{text}'."),
            };
        }
        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: false, out var key))
            throw new FormatException($"Unknown key '{parts[^1]}' in '{text}'.");
        return new KeyChord(key, modifiers);
    }
}
