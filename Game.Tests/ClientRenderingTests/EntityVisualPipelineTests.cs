using System.Numerics;
using System.Runtime.CompilerServices;
using Game.Client.Rendering.Entities;
using Game.Core.Animation;
using Game.Core.Assets;
using Game.Core.Combat;
using Game.Core.Inventory;
using Game.Core.Runtime;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class EntityVisualPipelineTests
{
    private static readonly RectI WideViewport = new(-2_000, -500, 4_000, 2_000);

    [Fact]
    public void Prepare_TransitionsThroughLocomotionCombatAndDeathStates()
    {
        var pipeline = new EntityVisualPipeline();
        var entities = new EntityFrameSnapshot[1];

        entities[0] = Enemy(1, "forest_boar", Vector2.Zero);
        pipeline.Prepare(entities, WideViewport, 10);
        Assert.Equal(EntityVisualState.Idle, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", new Vector2(30f, 0f));
        pipeline.Prepare(entities, WideViewport, 11);
        Assert.Equal(EntityVisualState.Walk, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", new Vector2(110f, 0f));
        pipeline.Prepare(entities, WideViewport, 12);
        Assert.Equal(EntityVisualState.Run, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", new Vector2(20f, -180f));
        pipeline.Prepare(entities, WideViewport, 13);
        Assert.Equal(EntityVisualState.Jump, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", Vector2.Zero, damageFlash: true);
        pipeline.Prepare(entities, WideViewport, 14);
        Assert.Equal(EntityVisualState.Hurt, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", Vector2.Zero);
        pipeline.Prepare(entities, WideViewport, 16);
        Assert.Equal(EntityVisualState.Hurt, pipeline.GetTrackedState(1));

        pipeline.NotifyAttack(1, 22);
        pipeline.Prepare(entities, WideViewport, 22);
        Assert.Equal(EntityVisualState.Attack, pipeline.GetTrackedState(1));
        pipeline.Prepare(entities, WideViewport, 25);
        Assert.Equal(EntityVisualState.Attack, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", Vector2.Zero, damageFlash: true);
        pipeline.Prepare(entities, WideViewport, 26);
        Assert.Equal(EntityVisualState.Hurt, pipeline.GetTrackedState(1));

        entities[0] = Enemy(1, "forest_boar", Vector2.Zero, active: false, health: 0);
        pipeline.Prepare(entities, WideViewport, 27);
        Assert.Equal(EntityVisualState.Death, pipeline.GetTrackedState(1));
    }

    [Fact]
    public void Prepare_ResolvesFlyingFacingSquashAndHitFlashVisuals()
    {
        var pipeline = new EntityVisualPipeline();
        var bird = Enemy(7, "forest_flock_bird", new Vector2(-42f, -15f), damageFlash: true);

        pipeline.Prepare(new[] { bird }, WideViewport, 80);

        Assert.Equal(EntityVisualState.Hurt, pipeline.GetTrackedState(7));
        Assert.True(pipeline.TryGetLastPose(7, out var pose));
        Assert.Equal(EntityVisualState.Hurt, pose.State);
        var sprite = FindCommand(pipeline.CommandBuffer, EntityVisualDrawCommandKind.Sprite);
        Assert.True(sprite.FlipHorizontally);
        Assert.Equal(EntityVisualColor.HitFlash, sprite.Color);
        Assert.True(sprite.Scale.X > 0f);
        Assert.True(sprite.Scale.Y > 0f);
    }

    [Fact]
    public void Prepare_CullsCorrectlyAcrossNegativeWorldCoordinates()
    {
        var pipeline = new EntityVisualPipeline();
        var entities = new[]
        {
            Enemy(1, "slime", Vector2.Zero, x: -930),
            Enemy(2, "slime", Vector2.Zero, x: 80)
        };

        var telemetry = pipeline.Prepare(entities, new RectI(-1_000, 0, 200, 200), 5);

        Assert.Equal(1, telemetry.VisibleEntities);
        Assert.Equal(1, telemetry.CulledEntities);
        Assert.Equal(2, telemetry.PreparedCommands);
        Assert.Equal(1, FindCommand(pipeline.CommandBuffer, EntityVisualDrawCommandKind.Sprite).EntityId);
    }

    [Fact]
    public void Prepare_EmitsShadowOutlineAndSpriteForEliteVariant()
    {
        var pipeline = new EntityVisualPipeline();

        var telemetry = pipeline.Prepare(
            new[] { Enemy(44, "crystal_cave_spider", new Vector2(10f, 0f)) },
            WideViewport,
            99);

        Assert.Equal(3, telemetry.PreparedCommands);
        Assert.Equal(EntityVisualDrawCommandKind.ShadowEllipse, pipeline.CommandBuffer[0].Kind);
        Assert.Equal(EntityVisualDrawCommandKind.Outline, pipeline.CommandBuffer[1].Kind);
        Assert.Equal(EntityVisualDrawCommandKind.Sprite, pipeline.CommandBuffer[2].Kind);
        Assert.Equal("entities/enemies/cave_spider_elite", pipeline.CommandBuffer[2].SpriteId);
        Assert.Equal(EntityVisualColor.Elite, pipeline.CommandBuffer[2].Color);
    }

    [Fact]
    public void Prepare_UsesProjectileRotationAndDroppedItemBobProfiles()
    {
        var pipeline = new EntityVisualPipeline();
        var entities = new[]
        {
            Projectile(8, "wooden_arrow", new Vector2(50f, 50f)),
            DroppedItem(9, "wood", new Vector2(-2f, 0f))
        };

        pipeline.Prepare(entities, WideViewport, 40);

        var projectile = FindSpriteForEntity(pipeline.CommandBuffer, 8);
        var droppedItem = FindSpriteForEntity(pipeline.CommandBuffer, 9);
        Assert.Equal(MathF.PI * 0.25f, projectile.RotationRadians, 3);
        Assert.Equal("items/wood", droppedItem.SpriteId);
        Assert.NotEqual(0f, droppedItem.RotationRadians);
    }

    [Fact]
    public void ConfigureRuntimeAnimations_SelectsEnemyCritterAndProjectileSourceFrames()
    {
        var spriteAssets = CreateRuntimeSpriteAssets();
        var pipeline = new EntityVisualPipeline();
        pipeline.Configure(CreateRuntimeAnimations(spriteAssets));
        var entities = new[]
        {
            Enemy(31, "hostile.runtime", Vector2.Zero),
            Enemy(32, "critter.runtime", Vector2.Zero, x: 50),
            Projectile(33, "projectile.runtime", new Vector2(30f, 40f), DamageType.Magic)
        };

        pipeline.Prepare(entities, WideViewport, 90);

        var enemy = FindSpriteForEntity(pipeline.CommandBuffer, 31);
        var critter = FindSpriteForEntity(pipeline.CommandBuffer, 32);
        var projectile = FindSpriteForEntity(pipeline.CommandBuffer, 33);
        Assert.Equal("test/runtime_enemy", enemy.SpriteId);
        Assert.Equal(2, enemy.FrameIndex);
        Assert.Equal(new Vector2(1.25f), enemy.Scale);
        AssertSourceRectangle(spriteAssets, enemy, 32, 0, 16, 16);
        Assert.Equal("test/runtime_critter", critter.SpriteId);
        Assert.Equal(1, critter.FrameIndex);
        AssertSourceRectangle(spriteAssets, critter, 16, 0, 16, 16);
        Assert.Equal("test/runtime_projectile", projectile.SpriteId);
        Assert.Equal(3, projectile.FrameIndex);
        Assert.Equal(EntityVisualColor.MagicProjectile, projectile.Color);
        AssertSourceRectangle(spriteAssets, projectile, 48, 0, 16, 16);
    }

    [Fact]
    public void ConfigureRuntimeAnimations_UsesRegistryThenLegacyAndContentFallbacks()
    {
        var spriteAssets = CreateRuntimeSpriteAssets();
        var pipeline = new EntityVisualPipeline();
        pipeline.Configure(CreateRuntimeAnimations(spriteAssets), ResolveModdedSprite);
        var entities = new[]
        {
            Enemy(41, "unregistered.hostile", Vector2.Zero),
            Enemy(42, "forest_boar", Vector2.Zero, x: 40),
            DroppedItem(43, "modded_relic", Vector2.Zero)
        };

        pipeline.Prepare(entities, WideViewport, 20);

        Assert.Equal(
            "test/runtime_enemy",
            FindSpriteForEntity(pipeline.CommandBuffer, 41).SpriteId);
        Assert.Equal(
            "entities/enemies/forest_boar",
            FindSpriteForEntity(pipeline.CommandBuffer, 42).SpriteId);
        Assert.Equal(
            "mods/items/relic",
            FindSpriteForEntity(pipeline.CommandBuffer, 43).SpriteId);
    }

    [Fact]
    public void Prepare_IsDeterministicForSameSnapshotsAndTickSequence()
    {
        var first = new EntityVisualPipeline();
        var second = new EntityVisualPipeline();
        var entities = new[]
        {
            Enemy(1, "forest_boar", new Vector2(94f, 0f), x: -80),
            Enemy(2, "firefly", new Vector2(-12f, 8f), x: 40),
            Projectile(3, "spark_bolt", new Vector2(70f, -30f))
        };

        for (var tick = 1L; tick <= 120; tick++)
        {
            first.Prepare(entities, WideViewport, tick);
            second.Prepare(entities, WideViewport, tick);
        }

        Assert.Equal(first.CommandBuffer.Count, second.CommandBuffer.Count);
        for (var index = 0; index < first.CommandBuffer.Count; index++)
        {
            Assert.Equal(first.CommandBuffer[index], second.CommandBuffer[index]);
        }
    }

    [Fact]
    public void Prepare_ReportsCommandAndTrackBudgetOverflowWithoutGrowing()
    {
        var pipeline = new EntityVisualPipeline(
            maximumTrackedEntities: 1,
            maximumDrawCommands: 2);
        var entities = new[]
        {
            Enemy(1, "crystal_cave_spider", Vector2.Zero),
            Enemy(2, "slime", Vector2.Zero, x: 40)
        };

        var telemetry = pipeline.Prepare(entities, WideViewport, 10);

        Assert.True(telemetry.WasBudgetClamped);
        Assert.Equal(1, telemetry.CommandOverflowEntities);
        Assert.Equal(1, telemetry.TrackOverflowEntities);
        Assert.Equal(2, telemetry.PreparedCommands);
        Assert.Equal(2, pipeline.CommandBuffer.Capacity);
    }

    [Fact]
    public void Prepare_TwoHundredEntitiesHasZeroSteadyStateAllocation()
    {
        var pipeline = new EntityVisualPipeline(maximumTrackedEntities: 256, maximumDrawCommands: 768);
        var entities = new EntityFrameSnapshot[200];
        for (var index = 0; index < entities.Length; index++)
        {
            var x = -1_500 + index * 15;
            var contentId = (index % 4) switch
            {
                0 => "forest_boar",
                1 => "meadow_rabbit",
                2 => "cave_bat_scout",
                _ => "cave_spider"
            };
            entities[index] = Enemy(index + 1, contentId, new Vector2(index % 3 * 30f, 0f), x: x);
        }

        const int warmupIterations = 256;
        for (var iteration = 0; iteration < warmupIterations; iteration++)
        {
            pipeline.Prepare(entities, WideViewport, iteration);
        }

        var allocated = MeasureUntilConsecutiveAllocationFreeWindows(
            pipeline,
            entities,
            warmupIterations);
        Assert.Equal(0, allocated);
        Assert.Equal(200, pipeline.CommandBuffer.Telemetry.VisibleEntities);
        Assert.False(pipeline.CommandBuffer.Telemetry.WasBudgetClamped);
    }

    [Fact]
    public void Prepare_RuntimeRegistryBindingsHaveZeroSteadyStateAllocation()
    {
        var pipeline = new EntityVisualPipeline(maximumTrackedEntities: 256, maximumDrawCommands: 512);
        pipeline.Configure(CreateRuntimeAnimations(CreateRuntimeSpriteAssets()));
        var entities = new EntityFrameSnapshot[200];
        for (var index = 0; index < entities.Length; index++)
        {
            entities[index] = Enemy(
                index + 1,
                index % 2 == 0 ? "hostile.runtime" : "critter.runtime",
                Vector2.Zero,
                x: -1_500 + index * 15);
        }

        const int warmupIterations = 256;
        for (var iteration = 0; iteration < warmupIterations; iteration++)
        {
            pipeline.Prepare(entities, WideViewport, iteration);
        }

        var allocated = MeasureUntilConsecutiveAllocationFreeWindows(
            pipeline,
            entities,
            warmupIterations);
        Assert.Equal(0, allocated);
        Assert.Equal(200, pipeline.CommandBuffer.Telemetry.VisibleEntities);
        Assert.False(pipeline.CommandBuffer.Telemetry.WasBudgetClamped);
    }

    private static long MeasureUntilConsecutiveAllocationFreeWindows(
        EntityVisualPipeline pipeline,
        EntityFrameSnapshot[] entities,
        long startingTick)
    {
        const int maximumWindows = 6;
        const int iterationsPerWindow = 1_000;
        var consecutiveAllocationFreeWindows = 0;
        var lastAllocatedBytes = long.MaxValue;
        for (var window = 0; window < maximumWindows; window++)
        {
            lastAllocatedBytes = MeasureAllocationWindow(
                pipeline,
                entities,
                startingTick + window * iterationsPerWindow,
                iterationsPerWindow);
            consecutiveAllocationFreeWindows = lastAllocatedBytes == 0
                ? consecutiveAllocationFreeWindows + 1
                : 0;
            if (consecutiveAllocationFreeWindows == 2)
            {
                return 0;
            }
        }

        return lastAllocatedBytes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureAllocationWindow(
        EntityVisualPipeline pipeline,
        EntityFrameSnapshot[] entities,
        long startingTick,
        int iterationCount)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < iterationCount; iteration++)
        {
            pipeline.Prepare(entities, WideViewport, startingTick + iteration);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static EntityVisualDrawCommand FindCommand(
        EntityVisualCommandBuffer buffer,
        EntityVisualDrawCommandKind kind)
    {
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index].Kind == kind)
            {
                return buffer[index];
            }
        }

        throw new InvalidOperationException($"Command kind {kind} was not found.");
    }

    private static EntityVisualDrawCommand FindSpriteForEntity(EntityVisualCommandBuffer buffer, int entityId)
    {
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index].Kind == EntityVisualDrawCommandKind.Sprite && buffer[index].EntityId == entityId)
            {
                return buffer[index];
            }
        }

        throw new InvalidOperationException($"Sprite command for entity {entityId} was not found.");
    }

    private static EntityFrameSnapshot Enemy(
        int id,
        string contentId,
        Vector2 velocity,
        int x = 0,
        bool damageFlash = false,
        bool active = true,
        int health = 20)
    {
        return new EntityFrameSnapshot(
            id,
            EntityFrameKind.Enemy,
            contentId,
            new Vector2(x, 40),
            velocity,
            new RectI(x, 40, 24, 20),
            active,
            health,
            20,
            ItemStack.Empty,
            damageFlash,
            DamageType.Generic);
    }

    private static EntityFrameSnapshot Projectile(
        int id,
        string contentId,
        Vector2 velocity,
        DamageType damageType = DamageType.Ranged)
    {
        return new EntityFrameSnapshot(
            id,
            EntityFrameKind.Projectile,
            contentId,
            new Vector2(10, 10),
            velocity,
            new RectI(10, 10, 8, 8),
            true,
            0,
            0,
            ItemStack.Empty,
            false,
            damageType);
    }

    private static EntityFrameSnapshot DroppedItem(int id, string contentId, Vector2 velocity)
    {
        return new EntityFrameSnapshot(
            id,
            EntityFrameKind.DroppedItem,
            contentId,
            new Vector2(10, 10),
            velocity,
            new RectI(10, 10, 10, 10),
            true,
            0,
            0,
            new ItemStack(contentId, 1),
            false,
            DamageType.Generic);
    }

    private static AnimationContentRegistry CreateRuntimeAnimations(SpriteAssetRegistry spriteAssets)
    {
        const string json = """
            {
              "runtimeAnimationContent": {
                "entityProfiles": [
                  {
                    "id": "runtime.enemy",
                    "kind": "Enemy",
                    "spriteId": "test/runtime_enemy",
                    "bindings": ["hostile.runtime"],
                    "motionStyle": "None",
                    "displayScale": 1.25,
                    "animations": [
                      { "state": "Idle", "startFrame": 2, "frameCount": 1, "ticksPerFrame": 4 }
                    ]
                  },
                  {
                    "id": "runtime.critter",
                    "kind": "Enemy",
                    "spriteId": "test/runtime_critter",
                    "bindings": ["critter.runtime"],
                    "motionStyle": "None",
                    "animations": [
                      { "state": "Idle", "startFrame": 1, "frameCount": 1, "ticksPerFrame": 6 }
                    ]
                  },
                  {
                    "id": "runtime.projectile",
                    "kind": "Projectile",
                    "spriteId": "test/runtime_projectile",
                    "bindings": ["projectile.runtime"],
                    "motionStyle": "RotateToVelocity",
                    "isFlying": true,
                    "castsShadow": false,
                    "animations": [
                      { "state": "Idle", "startFrame": 3, "frameCount": 1, "ticksPerFrame": 4 },
                      { "state": "Fly", "startFrame": 3, "frameCount": 1, "ticksPerFrame": 4 }
                    ]
                  }
                ],
                "fallbacks": [
                  { "kind": "Enemy", "profileId": "runtime.enemy" },
                  { "kind": "Projectile", "profileId": "runtime.projectile" }
                ]
              }
            }
            """;

        return new AnimationContentJsonLoader()
            .LoadFromJson(json, spriteAssets, "entity-visual-tests.json")
            .Registry;
    }

    private static SpriteAssetRegistry CreateRuntimeSpriteAssets()
    {
        return SpriteAssetRegistry.Create(
        [
            CreateRuntimeSheet("test/runtime_enemy", SpriteAssetCategory.Entity),
            CreateRuntimeSheet("test/runtime_critter", SpriteAssetCategory.Entity),
            CreateRuntimeSheet("test/runtime_projectile", SpriteAssetCategory.Projectile)
        ]);
    }

    private static SpriteAssetDefinition CreateRuntimeSheet(string id, SpriteAssetCategory category)
    {
        var frames = new SpriteFrameDefinition[4];
        for (var index = 0; index < frames.Length; index++)
        {
            frames[index] = new SpriteFrameDefinition
            {
                Id = $"frame_{index}",
                X = index * 16,
                Y = 0,
                Width = 16,
                Height = 16
            };
        }

        return new SpriteAssetDefinition
        {
            Id = id,
            Path = $"sprites/{id.Replace('/', '_')}.png",
            Category = category,
            Width = 64,
            Height = 16,
            Frames = frames
        };
    }

    private static void AssertSourceRectangle(
        SpriteAssetRegistry spriteAssets,
        EntityVisualDrawCommand command,
        int x,
        int y,
        int width,
        int height)
    {
        var sprite = spriteAssets.GetById(command.SpriteId);
        var frame = sprite.Frames[command.FrameIndex];
        Assert.Equal((x, y, width, height), (frame.X, frame.Y, frame.Width, frame.Height));
    }

    private static bool ResolveModdedSprite(in EntityFrameSnapshot entity, out string spriteId)
    {
        if (string.Equals(entity.ContentId, "modded_relic", StringComparison.OrdinalIgnoreCase))
        {
            spriteId = "mods/items/relic";
            return true;
        }

        spriteId = string.Empty;
        return false;
    }
}
