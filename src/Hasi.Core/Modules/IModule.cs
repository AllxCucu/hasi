using Hasi.Core.Diagnostics;
using Hasi.Core.Localization;
using Hasi.Core.Output;
using Hasi.Core.Power;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Speech;

namespace Hasi.Core.Modules;

/// <summary>
/// An independent feature (settings, e-mail, news, ...). The core only knows this interface;
/// it never references a concrete module.
/// </summary>
public interface IModule
{
    string Id { get; }

    /// <summary>Resource key of the name shown in the main menu.</summary>
    string TitleKey { get; }

    IScreen CreateRootScreen(IModuleContext context);

    /// <summary>
    /// Said or shown once after the welcome, when the module has something to tell (a document left unsaved by a
    /// serious error or a power cut); null when not.
    /// </summary>
    ScreenResult? OnStartup(IModuleContext context) => null;
}

public interface IModuleContext
{
    ILocalizer Localizer { get; }

    IShellServices Shell { get; }

    TimeProvider Time { get; }

    ApplicationInfo Application { get; }

    /// <summary>The same normalizer speech uses, for screens that show what will be spoken.</summary>
    ITextNormalizer Normalizer { get; }

    /// <summary>Voice comparison for the Diagnostic menu, or null when the voices to compare are not available.</summary>
    IVoiceComparison? VoiceComparison { get; }

    /// <summary>The user's settings (read, change, save), or null when the host has none.</summary>
    ISettingsService? Settings { get; }

    /// <summary>Diagnostic mode and copying its log, or null when the host has none.</summary>
    IDiagnosticsService? Diagnostics { get; }

    /// <summary>Starting HASI when the user signs in, or null when the host cannot.</summary>
    IAutostartService? Autostart { get; }

    /// <summary>Checking for and installing updates, or null when the host has none.</summary>
    Updates.UpdateFlow? Updates { get; }

    /// <summary>The user's personal data (drafts, recent documents), or null when the host keeps none.</summary>
    Storage.IUserDataStore? UserData { get; }

    /// <summary>Reading and writing .txt and .docx, or null when the host has no document support.</summary>
    Documents.IDocumentFormats? Documents { get; }

    /// <summary>The disk as HASI shows it (places, folders, files), or null when the host has none.</summary>
    Files.IFileSystem? Files { get; }
}

/// <summary>Shell features that modules may trigger.</summary>
public interface IShellServices
{
    /// <summary>The spoken "close HASI?" confirmation, the same one Alt+F4 uses.</summary>
    ScreenResult ConfirmExit();

    /// <summary>The spoken "restart the computer?" confirmation.</summary>
    ScreenResult ConfirmRestart();

    /// <summary>The spoken "shut the computer down?" confirmation.</summary>
    ScreenResult ConfirmShutdown();

    /// <summary>Applies a result that arrives later (see <see cref="Shell.Show"/>); on the shell's thread only.</summary>
    void Show(ScreenResult result, UtterancePriority priority = UtterancePriority.Interrupt);

    /// <summary>
    /// Runs <paramref name="proceed"/> at once when no open screen has unsaved work (<see cref="IUnsavedWork"/>);
    /// otherwise first asks what to do with it: save and continue, continue without saving (confirmed), or stay.
    /// Used before closing HASI, restarting or shutting down, and installing an update.
    /// </summary>
    ScreenResult GuardUnsaved(Func<ScreenResult> proceed);
}

/// <summary>Host operations the core may request, implemented by the application.</summary>
public interface ISystemActions
{
    /// <summary>Close HASI once the current speech has finished.</summary>
    void RequestExit();

    /// <summary>
    /// Once the current speech has finished, ask Windows to restart or shut down. HASI stays open and reports
    /// the outcome to the shell (<see cref="Shell.PowerStarted"/>, <see cref="Shell.PowerFailed"/>), unless the
    /// power action is only simulated: then HASI closes and the computer stays on.
    /// </summary>
    /// <param name="force">Close the other programs without letting them save (only after a second confirmation).</param>
    void RequestPower(PowerAction action, bool force);

    /// <summary>Restart and shut down only pretend ("Simulare oprire", <c>--simulate-power</c>).</summary>
    bool PowerIsSimulated { get; }

    /// <summary>Other programs with open windows, named before a restart or shutdown.</summary>
    IReadOnlyList<OpenProgram> OpenPrograms();
}

public sealed record ApplicationInfo(string Version);

public sealed record ModuleContext(
    ILocalizer Localizer,
    IShellServices Shell,
    ApplicationInfo Application,
    ITextNormalizer Normalizer,
    IVoiceComparison? VoiceComparison = null,
    ISettingsService? Settings = null,
    IDiagnosticsService? Diagnostics = null,
    IAutostartService? Autostart = null,
    Updates.UpdateFlow? Updates = null,
    TimeProvider? Time = null,
    Storage.IUserDataStore? UserData = null,
    Documents.IDocumentFormats? Documents = null,
    Files.IFileSystem? Files = null) : IModuleContext
{
    public TimeProvider Time { get; init; } = Time ?? TimeProvider.System;
}

public static class MainMenu
{
    public const string TitleKey = "mainMenu.title";

    public const string HelpKey = "mainMenu.help";

    /// <summary>Builds the main menu from the registered modules, in registration order.</summary>
    public static MenuScreen Create(IEnumerable<IModule> modules, IModuleContext context)
    {
        var items = modules
            .Select(module => new MenuItem(
                context.Localizer.Get(module.TitleKey),
                _ => ScreenResult.Push(module.CreateRootScreen(context))))
            .ToList();
        return new MenuScreen(context.Localizer.Get(TitleKey), items, help: context.Localizer.Get(HelpKey));
    }
}
