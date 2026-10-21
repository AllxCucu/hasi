using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Exit;

/// <summary>Closing HASI. Restarting and shutting down the computer come in phase 0.7.</summary>
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
        ]);
    }
}
