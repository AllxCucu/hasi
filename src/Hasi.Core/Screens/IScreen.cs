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
