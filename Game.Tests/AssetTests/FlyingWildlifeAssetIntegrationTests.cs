using Game.Core.Animation;
using Game.Core.Assets;
using Game.Core.Assets.Audit;
using Game.Core.Assets.Generation;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class FlyingWildlifeAssetIntegrationTests
{
    private static readonly WildlifeAssetContract[] Contracts =
    [
        new("meadow_butterfly", "entities/critters/meadow_butterfly"),
        new("forest_moth", "entities/critters/forest_moth"),
        new("cave_glowbug", "entities/critters/cave_glowbug")
    ];

    private static readonly string[] ExpectedFrameIds = ["flight_0", "flight_1", "flight_2", "flight_3"];

    [Fact]
    public void RepositoryFlyingWildlife_PreservesNativeFramesAndRuntimeBindings()
    {
        var dataRoot = FindRepositoryGameData();
        var database = new GameContentLoader().LoadFromRoot(dataRoot);
        var runtimeAnimations = Assert.IsType<AnimationContentRegistry>(database.RuntimeAnimations);
        var focusedDefinitions = new List<SpriteAssetDefinition>(Contracts.Length);

        foreach (var contract in Contracts)
        {
            var asset = database.SpriteAssets.GetById(contract.SpriteId);
            focusedDefinitions.Add(asset);

            Assert.Equal(64, asset.Width);
            Assert.Equal(16, asset.Height);
            Assert.Equal(8, asset.OriginX);
            Assert.Equal(8, asset.OriginY);
            Assert.Contains("flying_wildlife_polish_v1", asset.Provenance, StringComparison.Ordinal);
            Assert.True(asset.HasTag("native-16px"));
            Assert.Equal(ExpectedFrameIds, asset.Frames.Select(frame => frame.Id));
            Assert.All(asset.Frames, frame =>
            {
                Assert.Equal(16, frame.Width);
                Assert.Equal(16, frame.Height);
                Assert.Equal(8, frame.OriginX);
                Assert.Equal(8, frame.OriginY);
            });

            Assert.Equal(contract.SpriteId, database.Entities.GetById(contract.EntityId).TexturePath);
            var profile = runtimeAnimations.ResolveEntity(contract.EntityId, AnimationEntityKind.Enemy);
            Assert.False(profile.UsedFallback);
            Assert.Equal(contract.SpriteId, profile.Profile.SpriteId);
            Assert.True(profile.Profile.IsFlying);
            Assert.Equal(4, profile.Profile.GetAnimation(AnimationEntityVisualState.Fly).FrameCount);
        }

        var briefs = SpriteGenerationBriefRegistry.Create(
            new SpriteGenerationBriefJsonLoader().LoadBriefsFromDirectory(Path.Combine(dataRoot, "asset_briefs")));
        var report = new SpriteAssetAuditService().Audit(
            dataRoot,
            SpriteAssetRegistry.Create(focusedDefinitions),
            briefs);

        Assert.False(report.HasErrors, string.Join(Environment.NewLine, report.Issues.Select(issue => issue.Message)));
        Assert.Equal(Contracts.Length, report.Entries.Count);
        Assert.All(report.Entries, entry =>
        {
            Assert.Equal(SpriteAssetFileStatus.Present, entry.FileStatus);
            Assert.Equal(64, entry.ActualWidth);
            Assert.Equal(16, entry.ActualHeight);
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

    private readonly record struct WildlifeAssetContract(string EntityId, string SpriteId);
}
