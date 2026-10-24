using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

public enum EnterReason
{
    /// <summary>The first screen after HASI starts.</summary>
    Startup,

    /// <summary>The screen was just opened.</summary>
    Initial,

    /// <summary>The user came back to this screen from a deeper one.</summary>
    Return,
}

/// <summary>Navigation preferences shared by all screens.</summary>
public sealed class ShellOptions
{
    /// <summary>Jump from the last item to the first (and back). Off by default: beginners get lost.</summary>
    public bool WrapLists { get; set; }

    /// <summary>Announce each full local hour, after the current message. Off by default.</summary>
    public bool HourlyChime { get; set; }

    /// <summary>What is spoken while typing in text fields.</summary>
    public Settings.KeyEcho KeyEcho { get; set; } = Settings.KeyEcho.Character;

    /// <summary>Ctrl+Alt+H brings HASI back from any program; false when Windows did not give HASI the key.</summary>
    public bool ReturnHotkey { get; set; }
}

public interface IScreenContext
{
    ILocalizer Localizer { get; }

    ShellOptions Options { get; }
}

/// <summary>
/// One screen of the interface. Screens are pure logic: they get commands and return what should
/// happen. They never touch the window or the speech engine.
/// </summary>
/// <summary>A screen that receives every key, global ones included, instead of the shell acting on them ("Învață tastele").</summary>
public interface ICapturesAllKeys;

/// <summary>A screen where the user types text. While one is active, Num Lock on makes the numeric keypad type digits.</summary>
public interface ITextEntry;

public interface IScreen
{
    /// <summary>Name used in announcements and by "where am I".</summary>
    string Title { get; }

    /// <summary>What to say on entry: where you are, how many options, which one is selected.</summary>
    Utterance Enter(EnterReason reason, IScreenContext context);

    ScreenResult Handle(InputEvent input, IScreenContext context);

    /// <summary>Contextual help for F1 / Ctrl+H.</summary>
    Utterance Help(IScreenContext context);
}
