using System.Reflection;

namespace Hasi.Architecture.Tests;

public class DependencyRulesTests
{
    private static readonly string[] Modules =
        ["Hasi.Modules.Settings", "Hasi.Modules.KeyTutor", "Hasi.Modules.About", "Hasi.Modules.Exit"];

    private static string[] HasiReferences(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Hasi", StringComparison.Ordinal))
            .ToArray();

    private static string[] NonFrameworkReferences(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && n != "netstandard" && n != "mscorlib")
            .ToArray();

    [Fact]
    public void Core_depends_only_on_the_base_class_library()
    {
        Assert.Empty(NonFrameworkReferences("Hasi.Core"));
    }

    [Fact]
    public void Core_does_not_reference_windows_specific_assemblies()
    {
        var references = Assembly.Load("Hasi.Core").GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(references, n =>
            n.Contains("Windows", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Presentation", StringComparison.Ordinal)
            || n == "System.Speech");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Modules_depend_only_on_core(string module)
    {
        Assert.All(HasiReferences(module), r => Assert.Equal("Hasi.Core", r));
    }

    [Fact]
    public void Speech_depends_only_on_core_among_hasi_assemblies()
    {
        Assert.All(HasiReferences("Hasi.Speech"), r => Assert.Equal("Hasi.Core", r));
    }

    public static TheoryData<string> ModuleNames() => new(Modules);
}
