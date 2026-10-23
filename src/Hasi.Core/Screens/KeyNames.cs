using Hasi.Core.Input;
using Hasi.Core.Localization;

namespace Hasi.Core.Screens;

/// <summary>Spoken names of keys and typed characters ("Săgeata jos", "Control plus litera H", "virgulă").</summary>
public static class KeyNames
{
    private static readonly Dictionary<char, string> CharacterKeys = new()
    {
        [' '] = "char.space",
        [','] = "char.comma",
        ['.'] = "char.period",
        [':'] = "char.colon",
        [';'] = "char.semicolon",
        ['-'] = "char.hyphen",
        ['?'] = "char.question",
        ['!'] = "char.exclamation",
        ['"'] = "char.quote",
        ['\''] = "char.apostrophe",
        ['/'] = "char.slash",
        ['%'] = "char.percent",
        ['+'] = "char.plus",
        ['='] = "char.equals",
        ['('] = "char.parenOpen",
        [')'] = "char.parenClose",
        ['@'] = "char.at",
        ['€'] = "char.euro",
        ['&'] = "char.ampersand",
    };

    /// <summary>A typed character as echoed while writing: punctuation by name, letters and digits as they are.</summary>
    public static string Character(char c, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return CharacterKeys.TryGetValue(c, out var key) ? localizer.Get(key) : c.ToString();
    }

    /// <summary>A typed character as named by "Învață tastele": "Litera a", "Cifra 7", "virgulă".</summary>
    public static string CharacterKey(char c, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        if (char.IsLetter(c)) return localizer.Get("keyName.letter", ("letter", c));
        if (char.IsDigit(c)) return localizer.Get("keyName.digit", ("digit", c));
        return Character(c, localizer);
    }

    /// <summary>A key with its modifiers: "Control plus litera H", "Alt plus F4", "Săgeata jos".</summary>
    public static string Chord(KeyChord chord, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        var parts = new List<string>();
        if (chord.Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add(localizer.Get("keyName.ctrlPlus"));
        if (chord.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add(localizer.Get("keyName.shiftPlus"));
        if (chord.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add(localizer.Get("keyName.altPlus"));
        parts.Add(Key(chord.Key, localizer, lowerCase: parts.Count > 0));
        return string.Join(' ', parts);
    }

    private static string Key(Key key, ILocalizer localizer, bool lowerCase)
    {
        var name = key switch
        {
            >= Input.Key.A and <= Input.Key.Z => localizer.Get("keyName.letter", ("letter", key.ToString())),
            >= Input.Key.D0 and <= Input.Key.D9 => localizer.Get("keyName.digit", ("digit", key - Input.Key.D0)),
            >= Input.Key.NumPad0 and <= Input.Key.NumPad9 => localizer.Get("keyName.numpad", ("digit", key - Input.Key.NumPad0)),
            >= Input.Key.F1 and <= Input.Key.F12 => key.ToString(),
            _ => localizer.Get($"keyName.{key}"),
        };
        return lowerCase && name.Length > 1 && char.IsUpper(name[0]) && char.IsLower(name[1])
            ? char.ToLower(name[0], localizer.Culture) + name[1..]
            : name;
    }
}
