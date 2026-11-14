using System.Reflection;
using Arcade.Core.Content;

namespace Arcade.Tests;

/// <summary>Guards the layering: the generic engine parts must not depend on the football module or on MonoGame.</summary>
public sealed class ArchitectureTests
{
    private static readonly string[] GenericNamespaces =
    [
        "Arcade.Core.Common",
        "Arcade.Core.Simulation",
        "Arcade.Core.Input",
        "Arcade.Core.Content",
        "Arcade.Core.Localization",
    ];

    [Fact]
    public void Core_assembly_does_not_reference_monogame()
    {
        Assembly core = typeof(ContentRepository).Assembly;
        Assert.DoesNotContain(core.GetReferencedAssemblies(), name => name.Name!.StartsWith("MonoGame", StringComparison.Ordinal));
    }

    [Fact]
    public void Generic_engine_namespaces_do_not_use_football_types()
    {
        Assembly core = typeof(ContentRepository).Assembly;
        var offenders = new List<string>();
        foreach (Type type in core.GetTypes().Where(t => GenericNamespaces.Contains(t.Namespace)))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            IEnumerable<Type> used = type.GetFields(all).Select(f => f.FieldType)
                .Concat(type.GetProperties(all).Select(p => p.PropertyType))
                .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
            offenders.AddRange(used
                .Where(t => t.Namespace?.StartsWith("Arcade.Core.Football", StringComparison.Ordinal) == true)
                .Select(t => $"{type.FullName} -> {t.FullName}"));
        }

        Assert.Empty(offenders);
    }
}
