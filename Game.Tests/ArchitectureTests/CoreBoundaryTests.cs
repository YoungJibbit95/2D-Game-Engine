using System.Xml.Linq;
using Game.Core;
using Xunit;

namespace Game.Tests.ArchitectureTests;

public sealed class CoreBoundaryTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Game.Client",
        "MonoGame",
        "Microsoft.Xna.Framework"
    ];

    [Fact]
    public void GameCoreAssembly_DoesNotReferenceClientOrRenderingFramework()
    {
        var references = typeof(GameConstants).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            reference => ForbiddenAssemblyPrefixes.Any(prefix =>
                reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GameCoreProject_DoesNotReferenceClientOrMonoGamePackages()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "Game.Core", "Game.Core.csproj"));
        var references = document
            .Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            reference => ForbiddenAssemblyPrefixes.Any(prefix =>
                reference.Contains(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GameCoreSources_DoNotImportClientOrMonoGameNamespaces()
    {
        var coreRoot = Path.Combine(FindRepositoryRoot(), "Game.Core");
        var violations = Directory
            .EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file))
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (file, line, lineNumber: index + 1)))
            .Where(entry => entry.line.TrimStart().StartsWith("using Game.Client", StringComparison.Ordinal) ||
                            entry.line.TrimStart().StartsWith("using Microsoft.Xna.Framework", StringComparison.Ordinal))
            .Select(entry => $"{Path.GetRelativePath(coreRoot, entry.file)}:{entry.lineNumber}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PlayingState_DelegatesAuthoritativeFixedTickToGameSimulation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Game.Client",
            "GameStates",
            "PlayingState.cs"));

        Assert.Contains("_simulation.Tick(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_worldTime.Update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_player.Update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_entities.UpdateAll(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseSelectedItem(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PickupItems(", source, StringComparison.Ordinal);
    }

    private static bool IsGeneratedPath(string filePath)
    {
        var segments = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the YjsE repository root.");
    }
}
