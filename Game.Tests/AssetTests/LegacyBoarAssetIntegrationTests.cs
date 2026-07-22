using Game.Core.Assets;
using Game.Core.Assets.Audit;
using Game.Core.Assets.Generation;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class LegacyBoarAssetIntegrationTests
{
    private static readonly string[] ExpectedFrameIds = ["idle", "windup", "charge", "attack"];

    [Fact]
    public void RepositoryBoarFamily_PreservesNativeFramesAndRuntimeReferences()
    {
        var dataRoot = FindRepositoryGameData();
        var database = new GameContentLoader().LoadFromRoot(dataRoot);
        var normal = database.SpriteAssets.GetById("entities/enemies/forest_boar");
        var elite = database.SpriteAssets.GetById("entities/enemies/forest_boar_elite");

        AssertNativeSheet(normal);
        AssertNativeSheet(elite);
        Assert.Contains("legacy_boar_polish_v1", normal.Provenance, StringComparison.Ordinal);
        Assert.Contains("legacy_boar_polish_v1", elite.Provenance, StringComparison.Ordinal);

        Assert.Equal(normal.Id, database.Entities.GetById("forest_boar").TexturePath);
        Assert.Equal(elite.Id, database.Biomes.GetById("forest").Presentation.EliteSpriteId);

        var runtimeAnimations = Assert.IsType<Game.Core.Animation.AnimationContentRegistry>(database.RuntimeAnimations);
        var normalProfile = runtimeAnimations.GetEntity("forest_boar");
        var eliteProfile = runtimeAnimations.GetEntity("forest_boar_elite");
        Assert.Equal(normal.Id, normalProfile.SpriteId);
        Assert.Equal(normal.Id, eliteProfile.SpriteId);
        Assert.Equal(elite.Id, eliteProfile.EliteSpriteId);

        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(Path.Combine(dataRoot, "asset_briefs")));
        var focusedAssets = SpriteAssetRegistry.Create([normal, elite]);
        var report = new SpriteAssetAuditService().Audit(dataRoot, focusedAssets, briefs);

        Assert.False(report.HasErrors, string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));
        Assert.All(report.Entries, entry =>
        {
            Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
            Assert.Equal(128, entry.ActualWidth);
            Assert.Equal(32, entry.ActualHeight);
            Assert.True(entry.HasGenerationBrief);
        });
    }

    private static void AssertNativeSheet(SpriteAssetDefinition asset)
    {
        Assert.Equal(128, asset.Width);
        Assert.Equal(32, asset.Height);
        Assert.Equal(16, asset.OriginX);
        Assert.Equal(31, asset.OriginY);
        Assert.Equal(ExpectedFrameIds, asset.Frames.Select(frame => frame.Id));
        Assert.All(asset.Frames, frame =>
        {
            Assert.Equal(32, frame.Width);
            Assert.Equal(32, frame.Height);
            Assert.Equal(16, frame.OriginX);
            Assert.Equal(31, frame.OriginY);
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
