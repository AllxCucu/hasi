using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Core.Tests;

/// <summary>Small fixed screens so core tests do not depend on real modules.</summary>
internal static class TestScreens
{
    public static MenuScreen Fruits(Func<IScreenContext, ScreenResult>? onPear = null) => new(
        "Fructe",
        [new MenuItem("Măr", _ => ScreenResult.Push(new MessageScreen("Măr", "Un măr."))),
         new MenuItem("Pară", onPear),
         new MenuItem("Prună")]);

    public sealed class FakeModule(string titleKey, Func<IModuleContext, IScreen> root) : IModule
    {
        public string Id => TitleKey;

        public string TitleKey { get; } = titleKey;

        public IScreen CreateRootScreen(IModuleContext context) => root(context);
    }
}
