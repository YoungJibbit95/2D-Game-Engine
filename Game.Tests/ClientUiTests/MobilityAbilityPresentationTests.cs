using Game.Client.UI;
using Game.Core.Assets;
using Game.Core.Data;
using Game.Core.Equipment;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class MobilityAbilityPresentationTests
{
    [Fact]
    public void Planner_EmitsEnabledAbilitiesInStableSheetOrder()
    {
        var stats = PlayerStatBlock.Base with
        {
            CanDoubleJump = true,
            CanFly = true,
            CanGlide = true
        };
        var destination = new MobilityAbilityPresentation[MobilityAbilityDockPlanner.MaximumAbilityCount];

        var count = MobilityAbilityDockPlanner.Build(stats, destination);

        Assert.Equal(3, count);
        Assert.Equal(MobilityAbilityKind.DoubleJump, destination[0].Kind);
        Assert.Equal(MobilityAbilityKind.Flight, destination[1].Kind);
        Assert.Equal(MobilityAbilityKind.Glide, destination[2].Kind);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(MobilityAbilityDockPlanner.SpriteId, destination[index].SpriteId);
            Assert.Equal(index, destination[index].FrameIndex);
        }
    }

    [Theory]
    [InlineData(320, 180, 10)]
    [InlineData(640, 360, 6)]
    [InlineData(1280, 720, 10)]
    [InlineData(2560, 1440, 10)]
    public void Layout_IsContainedAndDoesNotOverlapStatusDock(int width, int height, int statusCount)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var feedback = PixelGameplayFeedbackLayoutPlanner.Resolve(viewport, statusCount);

        var mobility = MobilityAbilityDockPlanner.ResolveLayout(
            viewport,
            feedback.StatusDock,
            feedback.Density,
            MobilityAbilityDockPlanner.MaximumAbilityCount);

        AssertContained(viewport, mobility.Dock);
        Assert.True(mobility.Dock.Right <= feedback.StatusDock.X);
        for (var index = 0; index < mobility.Count; index++)
        {
            AssertContained(mobility.Dock, mobility.Slot(index));
        }

        Assert.Equal(Rectangle.Empty, mobility.Slot(-1));
        Assert.Equal(Rectangle.Empty, mobility.Slot(mobility.Count));
    }

    [Fact]
    public void Planning_IsAllocationFreeInSteadyState()
    {
        var stats = PlayerStatBlock.Base with
        {
            CanDoubleJump = true,
            CanFly = true,
            CanGlide = true
        };
        var destination = new MobilityAbilityPresentation[MobilityAbilityDockPlanner.MaximumAbilityCount];
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var feedback = PixelGameplayFeedbackLayoutPlanner.Resolve(viewport, statusEffectCount: 8);
        _ = MobilityAbilityDockPlanner.Build(stats, destination);
        _ = MobilityAbilityDockPlanner.ResolveLayout(
            viewport,
            feedback.StatusDock,
            feedback.Density,
            destination.Length);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var count = MobilityAbilityDockPlanner.Build(stats, destination);
            var layout = MobilityAbilityDockPlanner.ResolveLayout(
                viewport,
                feedback.StatusDock,
                feedback.Density,
                count);
            checksum += layout.Slot(iteration % count).X;
        }

        Assert.True(checksum > 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void RepositoryContent_BindsMobilityItemsAndSharedHudFrames()
    {
        var dataRoot = FindRepositoryGameData();
        var content = new GameContentLoader().LoadWithMods(dataRoot, modsRoot: null);
        Assert.False(
            content.Report.HasErrors,
            string.Join(Environment.NewLine, content.Report.Issues.Select(issue => issue.Message)));

        var sprites = SpriteAssetRegistry.Create(
            new SpriteAssetJsonLoader().LoadDefinitionsFromDirectory(Path.Combine(dataRoot, "assets")));
        var hud = sprites.GetById(MobilityAbilityDockPlanner.SpriteId);
        Assert.Equal(SpriteAssetCategory.Ui, hud.Category);
        Assert.Equal(48, hud.Width);
        Assert.Equal(16, hud.Height);
        Assert.Equal(new[] { "double_jump", "flight", "glide" }, hud.Frames.Select(frame => frame.Id));
        Assert.All(hud.Frames, frame =>
        {
            Assert.Equal(16, frame.Width);
            Assert.Equal(16, frame.Height);
        });

        Assert.Equal("items/double_jump_boots", content.Database.Items.GetById("double_jump_boots").TexturePath);
        Assert.Equal("items/skyward_wings", content.Database.Items.GetById("skyward_wings").TexturePath);
        Assert.Equal("items/ether_glider", content.Database.Items.GetById("ether_glider").TexturePath);
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

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
