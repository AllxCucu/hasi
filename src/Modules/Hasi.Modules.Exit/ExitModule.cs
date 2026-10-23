using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Exit;

/// <summary>Closing HASI, restarting or shutting down the computer; each asks for a spoken confirmation.</summary>
public sealed class ExitModule : IModule
{
    public string Id => "exit";

    public string TitleKey => "exit.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        var l = context.Localizer;
        return new MenuScreen(l.Get(TitleKey),
        [
            new MenuItem(l.Get("exit.closeHasi"), _ => context.Shell.ConfirmExit()),
            new MenuItem(l.Get("exit.restart"), _ => context.Shell.ConfirmRestart()),
            new MenuItem(l.Get("exit.shutdown"), _ => context.Shell.ConfirmShutdown()),
        ], help: l.Get("exit.help"));
    }
}
