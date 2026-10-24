using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.About;

/// <summary>
/// "Despre HASI": version, license and every attribution, read line by line.
/// Keep in sync with THIRD-PARTY-NOTICES.md.
/// </summary>
public sealed class AboutModule : IModule
{
    public string Id => "about";

    public string TitleKey => "about.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        var l = context.Localizer;
        MenuItem[] lines =
        [
            new(l.Get("about.name", ("version", context.Application.Version))),
            new(l.Get("about.purpose")),
            new(l.Get("about.license")),
            new(l.Get("about.rhvoice"), DisplayLabel: l.Get("about.rhvoice.display")),
            new(l.Get("about.paul")),
            new(l.Get("about.romanianData")),
            new(l.Get("about.multext")),
            new(l.Get("about.naudio")),
            new(l.Get("about.systemSpeech")),
            new(l.Get("about.dotnet")),
            new(l.Get("about.velopack")),
        ];
        return new MenuScreen(l.Get(TitleKey), lines, help: l.Get("about.help"), countKey: MenuScreen.LinesCountKey);
    }
}
