namespace Hasi.Core.Input;

/// <summary>
/// A set of bindings a screen can ask for on top of the default ones (<see cref="KeyMap.Resolve(KeyChord, KeyProfile)"/>).
/// Global commands (help, repeat, where am I, the time, exit, diagnostic) keep their keys in every profile.
/// </summary>
public enum KeyProfile
{
    Default,

    /// <summary>
    /// Editing text: arrows move by character and line, Ctrl+arrows by word, Home/End go to the start/end of the line,
    /// Ctrl+Home/End and Ctrl+Up/Down to the start/end of the document, Enter starts a new line, Delete deletes
    /// forward, Ctrl+S saves, Ctrl+Z/Ctrl+Y undo and redo, F10 and Ctrl+M open the screen's menu.
    /// </summary>
    TextEditing,
}
