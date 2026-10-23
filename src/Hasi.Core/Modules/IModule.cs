using Hasi.Core.Localization;
using Hasi.Core.Output;
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
}

public interface IModuleContext
{
    ILocalizer Localizer { get; }

    IShellServices Shell { get; }

    ApplicationInfo Application { get; }

    /// <summary>The same normalizer speech uses, for screens that show what will be spoken.</summary>
    ITextNormalizer Normalizer { get; }

    /// <summary>Voice comparison for the Diagnostic menu, or null when the voices to compare are not available.</summary>
    IVoiceComparison? VoiceComparison { get; }

    /// <summary>The user's settings (read, change, save), or null when the host has none.</summary>
    ISettingsService? Settings { get; }
}

/// <summary>Shell features that modules may trigger.</summary>
public interface IShellServices
{
    /// <summary>The spoken "close HASI?" confirmation, the same one Alt+F4 uses.</summary>
    ScreenResult ConfirmExit();
}

/// <summary>Host operations the core may request, implemented by the application.</summary>
public interface ISystemActions
{
    /// <summary>Close HASI once the current speech has finished.</summary>
    void RequestExit();
}

public sealed record ApplicationInfo(string Version);

public sealed record ModuleContext(
    ILocalizer Localizer,
    IShellServices Shell,
    ApplicationInfo Application,
    ITextNormalizer Normalizer,
    IVoiceComparison? VoiceComparison = null,
    ISettingsService? Settings = null) : IModuleContext;

public static class MainMenu
{
    public const string TitleKey = "mainMenu.title";

    /// <summary>Builds the main menu from the registered modules, in registration order.</summary>
    public static MenuScreen Create(IEnumerable<IModule> modules, IModuleContext context)
    {
        var items = modules
            .Select(module => new MenuItem(
                context.Localizer.Get(module.TitleKey),
                _ => ScreenResult.Push(module.CreateRootScreen(context))))
            .ToList();
        return new MenuScreen(context.Localizer.Get(TitleKey), items);
    }
}
