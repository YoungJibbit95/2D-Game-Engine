using Game.Core.Biomes;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.PerformanceTests;

public sealed class GameFrameSnapshotAllocationTests
{
    private const int EntityCount = 24;
    private const int WarmupTicks = 64;
    private const int MeasurementTicks = 180;

    [Fact]
    public void MovingEntityFrameSnapshot_PhaseStaysWithinAllocationBudget()
    {
        var content = new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(Array.Empty<ItemDefinition>()),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
        var entities = new EntityManager(spatialCellSize: 64);
        for (var index = 0; index < EntityCount; index++)
        {
            entities.Add(new MovingSnapshotEntity(new Vector2(index * 16, 32)));
        }

        using var simulation = new GameSimulation(
            content,
            new World(64, 16, WorldMetadata.CreateDefault(seed: 7)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            options: new GameSimulationOptions
            {
                AutoPickupItems = false,
                EnablePhaseTelemetry = true,
                EnemySpawnRateMultiplier = 0
            });

        for (var tick = 0; tick < WarmupTicks; tick++)
        {
            simulation.Tick(PlayerCommand.None, 1f / 60f);
        }

        simulation.PhaseTelemetry.Reset();
        for (var tick = 0; tick < MeasurementTicks; tick++)
        {
            simulation.Tick(PlayerCommand.None, 1f / 60f);
        }

        var telemetry = simulation.PhaseTelemetry.CaptureSnapshot();
        Assert.True(telemetry.TryGet(GameSimulationPhase.FrameSnapshot, out var frameSnapshot));
        Assert.Equal(MeasurementTicks, frameSnapshot.Samples);
        Assert.True(
            frameSnapshot.AverageAllocatedBytes <= 2_400,
            $"FrameSnapshot allocated {frameSnapshot.AverageAllocatedBytes:0.0} B/tick for {EntityCount} moving entities.");
    }

    private sealed class MovingSnapshotEntity : Entity
    {
        public MovingSnapshotEntity(Vector2 position)
        {
            Position = position;
        }

        public override RectI Bounds => new((int)Position.X, (int)Position.Y, 12, 12);

        public override void Update(World world, float deltaSeconds)
        {
            Position += new Vector2(deltaSeconds * 60f, 0);
        }
    }
}
