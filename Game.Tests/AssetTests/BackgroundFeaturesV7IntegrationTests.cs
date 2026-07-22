using Game.Core.Assets;
using Game.Core.Assets.Audit;
using Game.Core.Assets.Generation;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class BackgroundFeaturesV7IntegrationTests
{
    private static readonly string[] ExpectedSpriteIds =
    [
        "world/backgrounds/features_v7/forest_mountains",
        "world/backgrounds/features_v7/forest_floating_islands",
        "world/backgrounds/features_v7/amber_mountains",
        "world/backgrounds/features_v7/amber_floating_islands",
        "world/backgrounds/features_v7/twilight_mountains",
        "world/backgrounds/features_v7/twilight_floating_islands",
        "world/backgrounds/features_v7/crystal_mountains",
        "world/backgrounds/features_v7/crystal_floating_islands"
    ];

    [Fact]
    public void RepositoryBackgroundFeatures_AreNativeAuditedRuntimeAssets()
    {
        var dataRoot = FindRepositoryGameData();
        var database = new GameContentLoader().LoadFromRoot(dataRoot);
        var assets = ExpectedSpriteIds
            .Select(database.SpriteAssets.GetById)
            .ToArray();

        Assert.All(assets, asset =>
        {
            Assert.Equal(1024, asset.Width);
            Assert.Equal(256, asset.Height);
            Assert.Equal(0, asset.OriginX);
            Assert.Equal(255, asset.OriginY);
            Assert.StartsWith("assets/BackgroundFeaturesV7/", asset.Path, StringComparison.Ordinal);
            Assert.Contains("background_features_v7", asset.Provenance, StringComparison.Ordinal);
        });

        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(
                Path.Combine(dataRoot, "asset_briefs")));
        var report = new SpriteAssetAuditService().Audit(
            dataRoot,
            SpriteAssetRegistry.Create(assets),
            briefs);

        Assert.False(
            report.HasErrors,
            string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));
        Assert.All(report.Entries, entry =>
        {
            Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
            Assert.Equal(1024, entry.ActualWidth);
            Assert.Equal(256, entry.ActualHeight);
            Assert.True(entry.HasGenerationBrief);
        });
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(Path.Combine(candidate, "assets")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
