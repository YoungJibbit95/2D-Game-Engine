using System.Text.Json;
using Game.Core.Diagnostics.Performance;
using Xunit;

namespace Game.Tests.PerformanceTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LongSessionPerformanceCollection
{
    public const string Name = "Long session performance";
}

internal static class LongSessionDistributionArtifactExporter
{
    public const string ExportDirectoryEnvironmentVariable = "YJSE_LONG_SESSION_DISTRIBUTION_EXPORT_DIRECTORY";

    public static void ExportIfRequested(
        string artifactName,
        params LongSessionDistributionSnapshot[] distributions)
    {
        var exportDirectory = Environment.GetEnvironmentVariable(ExportDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(exportDirectory))
        {
            return;
        }

        LongSessionDistributionLabel.Validate(artifactName);
        Array.Sort(
            distributions,
            static (first, second) => StringComparer.Ordinal.Compare(first.Label, second.Label));

        var fullDirectory = Path.GetFullPath(exportDirectory);
        Directory.CreateDirectory(fullDirectory);
        var artifact = new LongSessionDistributionArtifact(
            SchemaVersion: 1,
            Scenario: artifactName,
            Distributions: distributions);
        File.WriteAllText(
            Path.Combine(fullDirectory, artifactName + ".json"),
            JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}

internal sealed record LongSessionDistributionArtifact(
    int SchemaVersion,
    string Scenario,
    IReadOnlyList<LongSessionDistributionSnapshot> Distributions);
