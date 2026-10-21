using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.KeyTutor;

/// <summary>"Învață tastele": press any key to hear what it does. Implemented in phase 0.7.</summary>
public sealed class KeyTutorModule : IModule
{
    public string Id => "keyTutor";

    public string TitleKey => "keyTutor.title";

    public IScreen CreateRootScreen(IModuleContext context) =>
        new MessageScreen(context.Localizer.Get(TitleKey), context.Localizer.Get("common.notYetAvailable"));
}
