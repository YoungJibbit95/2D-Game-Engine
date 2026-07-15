using Game.Client.Rendering;
using Game.Client.Rendering.Effects;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class WaterReflectionSurfacePlannerTests
{
    [Fact]
    public void Build_CoalescesVisibleLiquidSurfaceWithinViewportAndBudget()
    {
        var world = new World(32, 16, WorldMetadata.CreateDefault(91));
        for (var x = 4; x <= 10; x++)
        {
            world.SetTile(x, 6, TileInstance.Liquid(byte.MaxValue));
        }

        var viewport = new Rectangle(0, 0, 320, 180);
        var camera = new Camera2D { Position = new Vector2(7.5f * 16f, 6f * 16f), Zoom = 2f };
        camera.Recalculate(viewport);
        var profile = PresentationQualityProfile.Create(PresentationQualityTier.Medium, viewport);
        var surfaces = new WaterReflectionSurface[profile.Budget.MaxReflectionSurfaces];

        var telemetry = WaterReflectionSurfacePlanner.Build(world, camera, viewport, profile, surfaces);

        Assert.InRange(telemetry.SurfaceCount, 1, profile.Budget.MaxReflectionSurfaces);
        for (var index = 0; index < telemetry.SurfaceCount; index++)
        {
            var bounds = surfaces[index].ScreenBounds;
            Assert.True(bounds.Left >= viewport.Left);
            Assert.True(bounds.Top >= viewport.Top);
            Assert.True(bounds.Right <= viewport.Right);
            Assert.True(bounds.Bottom <= viewport.Bottom);
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
        }
    }

    [Fact]
    public void Build_RespectsCallerSurfaceCapacity()
    {
        var world = new World(32, 16, WorldMetadata.CreateDefault(92));
        world.SetTile(4, 5, TileInstance.Liquid(byte.MaxValue));
        world.SetTile(12, 5, TileInstance.Liquid(byte.MaxValue));
        var viewport = new Rectangle(0, 0, 512, 256);
        var camera = new Camera2D { Position = new Vector2(8f * 16f, 5f * 16f), Zoom = 1f };
        camera.Recalculate(viewport);
        var profile = PresentationQualityProfile.Create(PresentationQualityTier.High, viewport);
        var surfaces = new WaterReflectionSurface[1];

        var telemetry = WaterReflectionSurfacePlanner.Build(world, camera, viewport, profile, surfaces);

        Assert.Equal(1, telemetry.SurfaceCount);
        Assert.True(telemetry.WasBudgetClamped);
    }

    [Fact]
    public void Build_ReusesCallerSurfaceBufferWithoutSteadyStateAllocation()
    {
        var world = new World(32, 16, WorldMetadata.CreateDefault(93));
        for (var x = 3; x < 20; x++)
        {
            world.SetTile(x, 6, TileInstance.Liquid(byte.MaxValue));
        }

        var viewport = new Rectangle(0, 0, 512, 256);
        var camera = new Camera2D { Position = new Vector2(10f * 16f, 6f * 16f), Zoom = 1f };
        camera.Recalculate(viewport);
        var profile = PresentationQualityProfile.Create(PresentationQualityTier.High, viewport);
        var surfaces = new WaterReflectionSurface[profile.Budget.MaxReflectionSurfaces];
        _ = WaterReflectionSurfacePlanner.Build(world, camera, viewport, profile, surfaces);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = WaterReflectionSurfacePlanner.Build(world, camera, viewport, profile, surfaces);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
