using Game.Client.Rendering;
using Game.Client.Rendering.Effects;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LiquidPresentationPlannerTests
{
    [Fact]
    public void Build_CoalescesSurfaceRunAndPreparesDepthHighlightAndShoreCommands()
    {
        var world = CreateWorld();
        for (var x = 4; x <= 10; x++)
        {
            world.SetTile(x, 6, TileInstance.Liquid(byte.MaxValue));
        }

        var viewport = new Rectangle(0, 0, 320, 180);
        var camera = CreateCamera(viewport, 7.5f, 6f);
        var palette = WaterPresentationPaletteCatalog.Resolve("twilight_marsh");
        var commands = new LiquidPresentationCommand[32];

        var telemetry = LiquidPresentationPlanner.Build(
            world,
            camera,
            viewport,
            palette,
            opacity: 1f,
            tickNumber: 120,
            commands);

        Assert.Equal(1, telemetry.CommandCount);
        Assert.Equal(1, telemetry.SurfaceRuns);
        Assert.Equal(2, telemetry.ShoreTransitions);
        Assert.False(telemetry.WasBudgetClamped);
        var command = commands[0];
        Assert.True(command.IsExposedSurface);
        Assert.True(command.BodyBounds.Width >= 7 * 16);
        Assert.False(command.DepthBandBounds.IsEmpty);
        Assert.False(command.SurfaceHighlightBounds.IsEmpty);
        Assert.False(command.LeftShoreBounds.IsEmpty);
        Assert.False(command.RightShoreBounds.IsEmpty);
        Assert.Equal(palette.ShallowColor, command.BodyColor);
    }

    [Fact]
    public void Build_UsesBiomePaletteAndDarkensDeeperRuns()
    {
        var world = CreateWorld();
        for (var y = 4; y <= 8; y++)
        {
            for (var x = 6; x <= 9; x++)
            {
                world.SetTile(x, y, TileInstance.Liquid(byte.MaxValue));
            }
        }

        var viewport = new Rectangle(0, 0, 320, 240);
        var camera = CreateCamera(viewport, 8f, 6f);
        var palette = WaterPresentationPaletteCatalog.Resolve("amber_grove");
        var commands = new LiquidPresentationCommand[32];

        var telemetry = LiquidPresentationPlanner.Build(
            world,
            camera,
            viewport,
            palette,
            opacity: 1f,
            tickNumber: 44,
            commands);

        Assert.Equal(5, telemetry.CommandCount);
        Assert.Equal(palette.ShallowColor, commands[0].BodyColor);
        Assert.NotEqual(commands[0].BodyColor, commands[4].BodyColor);
        Assert.True(commands[4].BodyColor.R < commands[0].BodyColor.R);
        Assert.True(commands[4].BodyColor.G < commands[0].BodyColor.G);
    }

    [Fact]
    public void Build_RespectsCallerCapacity()
    {
        var world = CreateWorld();
        for (var x = 2; x < 18; x += 2)
        {
            world.SetTile(x, 6, TileInstance.Liquid(byte.MaxValue));
        }

        var viewport = new Rectangle(0, 0, 320, 180);
        var camera = CreateCamera(viewport, 10f, 6f);
        var palette = WaterPresentationPaletteCatalog.ClearWater;
        var commands = new LiquidPresentationCommand[2];

        var telemetry = LiquidPresentationPlanner.Build(
            world,
            camera,
            viewport,
            palette,
            opacity: 0.72f,
            tickNumber: 1,
            commands);

        Assert.Equal(commands.Length, telemetry.CommandCount);
        Assert.True(telemetry.WasBudgetClamped);
    }

    [Fact]
    public void Build_ReusesCallerBufferWithoutSteadyStateAllocation()
    {
        var world = CreateWorld();
        for (var y = 5; y <= 9; y++)
        {
            for (var x = 3; x < 20; x++)
            {
                world.SetTile(x, y, TileInstance.Liquid(byte.MaxValue));
            }
        }

        var viewport = new Rectangle(0, 0, 512, 256);
        var camera = CreateCamera(viewport, 10f, 7f);
        var palette = WaterPresentationPaletteCatalog.Resolve("twilight_marsh");
        var commands = new LiquidPresentationCommand[64];
        _ = LiquidPresentationPlanner.Build(world, camera, viewport, palette, 0.72f, 60, commands);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = LiquidPresentationPlanner.Build(world, camera, viewport, palette, 0.72f, iteration, commands);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ReflectionPlanner_UsesSameBiomePalette()
    {
        var world = CreateWorld();
        for (var x = 4; x <= 10; x++)
        {
            world.SetTile(x, 6, TileInstance.Liquid(byte.MaxValue));
        }

        var viewport = new Rectangle(0, 0, 320, 180);
        var camera = CreateCamera(viewport, 7.5f, 6f);
        var quality = PresentationQualityProfile.Create(PresentationQualityTier.High, viewport);
        var palette = WaterPresentationPaletteCatalog.Resolve("amber_grove");
        var surfaces = new WaterReflectionSurface[quality.Budget.MaxReflectionSurfaces];

        var telemetry = WaterReflectionSurfacePlanner.Build(
            world,
            camera,
            viewport,
            quality,
            palette,
            surfaces);

        Assert.True(telemetry.SurfaceCount > 0);
        Assert.Equal(palette.ReflectionTint, surfaces[0].Tint);
        Assert.Equal(palette.WaterReflectivity, surfaces[0].Reflectivity);
    }

    private static World CreateWorld()
    {
        return new World(32, 16, WorldMetadata.CreateDefault(404));
    }

    private static Camera2D CreateCamera(Rectangle viewport, float tileX, float tileY)
    {
        var camera = new Camera2D
        {
            Position = new Vector2(tileX * 16f, tileY * 16f),
            Zoom = 1f
        };
        camera.Recalculate(viewport);
        return camera;
    }
}
