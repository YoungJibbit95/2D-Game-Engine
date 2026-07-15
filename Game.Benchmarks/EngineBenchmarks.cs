using BenchmarkDotNet.Attributes;
using Game.Core;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Inventory;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Runtime;
using Game.Core.Sessions;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using System.Numerics;

namespace Game.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("world", "generation")]
public class InfiniteWorldGenerationBenchmarks
{
    private const int Seed = 73013;
    private InfiniteWorldChunkGenerator _warmGenerator = null!;
    private WorldGenerationProfile _profile = null!;

    [Params(-4096, 0, 4096)]
    public int ChunkX { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _profile = WorldGenerationProfile.Small;
        _warmGenerator = new InfiniteWorldChunkGenerator();
        _warmGenerator.GenerateChunk(_profile, Seed, new ChunkPos(ChunkX - 1, 2));
    }

    [Benchmark]
    public Chunk ColdNewGenerator()
    {
        return new InfiniteWorldChunkGenerator().GenerateChunk(_profile, Seed, new ChunkPos(ChunkX, 2));
    }

    [Benchmark(Baseline = true)]
    public Chunk WarmReusedGenerator()
    {
        return _warmGenerator.GenerateChunk(_profile, Seed, new ChunkPos(ChunkX, 2));
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("world", "streaming")]
public class ChunkStreamingBenchmarks
{
    private StreamingScenario? _warmScenario;

    [Benchmark]
    public int ColdWindowAcrossNegativeAndPositiveX()
    {
        var scenario = StreamingScenario.Create();
        try
        {
            return scenario.RunTrace([-128, 0, 128]);
        }
        finally
        {
            scenario.Cancel();
        }
    }

    [IterationSetup(Target = nameof(WarmCameraTraceAcrossOrigin))]
    public void PrepareWarmTrace()
    {
        _warmScenario = StreamingScenario.Create();
        _warmScenario.RunTrace([-128]);
    }

    [Benchmark(Baseline = true)]
    public int WarmCameraTraceAcrossOrigin()
    {
        return _warmScenario!.RunTrace([-96, -32, 32, 96, 128]);
    }

    [IterationCleanup(Target = nameof(WarmCameraTraceAcrossOrigin))]
    public void CleanupWarmTrace()
    {
        _warmScenario?.Cancel();
        _warmScenario = null;
    }

    private sealed class StreamingScenario
    {
        private static readonly ChunkStreamingOptions Options = new()
        {
            LoadMarginChunks = 1,
            UnloadMarginChunks = 3,
            MaxChunkOperationsPerUpdate = 12,
            MaxConcurrentLoadJobs = 4,
            MaxConcurrentSaveJobs = 1,
            MaxApplyQueueLength = 24
        };

        private readonly World _world;
        private readonly WorldGenerationProfile _profile;
        private readonly ChunkStreamingService _streaming;

        private StreamingScenario(
            World world,
            WorldGenerationProfile profile,
            ChunkStreamingService streaming)
        {
            _world = world;
            _profile = profile;
            _streaming = streaming;
        }

        public static StreamingScenario Create()
        {
            var profile = WorldGenerationProfile.Small;
            var generator = new InfiniteWorldChunkGenerator();
            return new StreamingScenario(
                generator.CreateWorld(profile, 73013, "Benchmark Stream"),
                profile,
                new ChunkStreamingService(generator: generator));
        }

        public int RunTrace(ReadOnlySpan<int> centers)
        {
            var checksum = 0;
            for (var index = 0; index < centers.Length; index++)
            {
                checksum = unchecked(checksum * 397 + SettleWindow(centers[index]));
            }

            return checksum;
        }

        public void Cancel()
        {
            _streaming.CancelPendingJobs();
        }

        private int SettleWindow(int centerX)
        {
            var visible = new RectI(centerX - 48, _profile.SurfaceBaseY - 20, 96, 48);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            ChunkStreamingUpdateResult result;
            do
            {
                result = _streaming.Update(_world, _profile, visible, options: Options);
                var telemetry = result.Telemetry;
                if (result.Plan.RequiredChunks.All(position => _world.TryGetChunk(position, out _)) &&
                    telemetry.PendingLoadJobs == 0 &&
                    telemetry.ApplyQueueLength == 0)
                {
                    return _world.Chunks.Count + result.ApplyOperationsProcessed;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Streaming did not settle at tile X {centerX}.");
                }

                Thread.Yield();
            }
            while (true);
        }
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("rendering", "lighting")]
public class LightingBenchmarks
{
    private LightingSystem _lighting = null!;
    private World _world = null!;
    private GameContentDatabase _content = null!;

    [Params(4, 12)]
    public int DirtyChunkBudget { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _content = BenchmarkContent.Load();
        _world = new SimpleWorldGenerator().Generate(256, 128, 41771);
        var torch = _content.Tiles.GetById("torch");
        for (var x = 24; x < 232; x += 24)
        {
            _world.SetTile(x, 50, torch.NumericId);
        }

        _lighting = new LightingSystem();
    }

    [IterationSetup]
    public void MarkDirty()
    {
        foreach (var chunk in _world.Chunks.Values)
        {
            chunk.MarkLightDirty();
        }
    }

    [Benchmark]
    public LightingUpdateResult RecalculateDirtyRegions()
    {
        return _lighting.RecalculateDirty(
            _world,
            _content.Tiles,
            LightingSystem.ResolveSunlight(0.5),
            maxChunks: DirtyChunkBudget);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("entities", "spawning")]
public class SpawnSchedulerBenchmarks
{
    private GameContentDatabase _content = null!;
    private World _world = null!;
    private EntityManager _entities = null!;
    private SpawnScheduler _scheduler = null!;
    private WorldTime _time = null!;
    private SpawnActivitySource[] _sources = null!;
    private SpawnSchedulerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _content = BenchmarkContent.Load();
        _time = new WorldTime();
        _sources =
        [
            SpawnActivitySource.ForPlayer(
                1,
                new TilePos(-80, 39),
                RectI.FromInclusiveTileBounds(-96, 28, -64, 50),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f)),
            SpawnActivitySource.ForCamera(
                2,
                new TilePos(80, 39),
                RectI.FromInclusiveTileBounds(64, 28, 96, 50),
                new SpawnEnvironment("forest", "surface", "Clear", null, 1f))
        ];
        _options = new SpawnSchedulerOptions
        {
            SpawnIntervalSeconds = 0.01f,
            AttemptsPerInterval = 32,
            MinDistanceTiles = 18,
            MaxDistanceTiles = 64,
            VerticalSearchRadiusTiles = 24,
            PlacementSearchRadiusTiles = 24,
            SectorCount = 16,
            MaxTotalActiveEnemies = 200,
            DespawnDistanceTiles = 256
        };
    }

    [IterationSetup]
    public void PreparePopulation()
    {
        var generator = new InfiniteWorldChunkGenerator();
        var profile = WorldGenerationProfile.Small;
        _world = generator.CreateWorld(profile, 81772, "Spawn Benchmark");
        var surfaceChunkY = Math.Max(0, profile.SurfaceBaseY / GameConstants.ChunkSize);
        for (var chunkX = -8; chunkX <= 8; chunkX++)
        {
            for (var chunkY = Math.Max(0, surfaceChunkY - 2); chunkY <= surfaceChunkY + 2; chunkY++)
            {
                generator.EnsureChunk(_world, profile, new ChunkPos(chunkX, chunkY));
            }
        }

        _entities = new EntityManager(64);
        var factory = new EntityFactory(new TileCollisionResolver());
        var definitions = _content.Entities.Definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < 200; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            var x = side * (48 + (index % 80));
            var definition = definitions[index % definitions.Length];
            _entities.Add(factory.CreateEnemy(definition, new Vector2(x * GameConstants.TileSize, 38 * GameConstants.TileSize)));
        }

        _scheduler = new SpawnScheduler(new Random(1977));
    }

    [Benchmark]
    public SpawnSchedulerResult MaintainTwoHundredEntitiesAcrossTwoActivitySources()
    {
        return _scheduler.Update(
            _world,
            _entities,
            _content,
            new BiomeMap("forest"),
            _time,
            _sources,
            1f / 60f,
            _options);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("entities", "ai")]
public class EntityAiBenchmarks
{
    private const int EntityCount = 200;
    private CritterAiBehavior[] _behaviors = null!;
    private EnemyEntity[] _actors = null!;
    private AiUpdateContext _context;

    [GlobalSetup]
    public void Setup()
    {
        var world = new World(512, 32, WorldMetadata.CreateDefault(4441));
        for (var x = 0; x < 512; x++)
        {
            world.SetTile(x, 10, KnownTileIds.Dirt);
        }

        var profile = new AiProfileDefinition
        {
            Kind = AiBehaviorKind.Critter,
            MoveSpeed = 30,
            FleeSpeed = 70,
            PatrolRadius = 120,
            FlockRadius = 48,
            FlockWeight = 0.6f,
            MinFlockSize = 3,
            IdleChance = 0.1f,
            DecisionInterval = 0.5f,
            RequiresLineOfSight = false,
            AvoidLedges = false,
            AvoidLiquid = false
        };
        _behaviors = new CritterAiBehavior[EntityCount];
        _actors = new EnemyEntity[EntityCount];
        var manager = new EntityManager(32);
        for (var index = 0; index < EntityCount; index++)
        {
            var behavior = new CritterAiBehavior(profile);
            var actor = new EnemyEntity(
                "benchmark_critter",
                new Vector2((index + 8) * GameConstants.TileSize, 64),
                new Vector2(12, 10),
                new Game.Core.Combat.HealthComponent(10),
                behavior,
                new TileCollisionResolver(),
                contactDamage: 0,
                faction: EntityFaction.Friendly,
                movementMode: EntityMovementMode.Flying,
                tags: ["living_world", "benchmark"]);
            _behaviors[index] = behavior;
            _actors[index] = actor;
            manager.Add(actor);
        }

        _context = new AiUpdateContext(world, manager.Entities, IsNight: false);
        for (var warmup = 0; warmup < 32; warmup++)
        {
            UpdateAll();
        }
    }

    [Benchmark]
    public long UpdateTwoHundredCritters()
    {
        UpdateAll();
        return _behaviors[0].Telemetry.UpdateCount + _behaviors[^1].Telemetry.UpdateCount;
    }

    private void UpdateAll()
    {
        for (var index = 0; index < _behaviors.Length; index++)
        {
            _behaviors[index].Update(_actors[index], _context, 1f / 60f);
        }
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("simulation", "telemetry")]
public class SimulationTelemetryBenchmarks
{
    private LoadedGameSession _session = null!;

    [Params(false, true)]
    public bool EnablePhaseTelemetry { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _session = Program.CreateRepresentativeSession(BenchmarkContent.Load());
        _session.Simulation.ConfigureOptions(
            _session.Simulation.Options with { EnablePhaseTelemetry = EnablePhaseTelemetry });
        for (var index = 0; index < 120; index++)
        {
            _session.Simulation.Tick(Program.CommandForTick(index), 1f / 60f);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
    }

    [Benchmark]
    public int FixedTick()
    {
        var simulation = _session.Simulation;
        var result = simulation.Tick(Program.CommandForTick(simulation.TickNumber), 1f / 60f);
        return result.Snapshot.Entities.Count + result.Snapshot.Hud.TotalInventoryItems;
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("simulation", "snapshot")]
public class FrameSnapshotQueryBenchmarks
{
    private ImmutableSnapshotList<EntityFrameSnapshot> _entities = null!;

    [Params(0, 32, 200)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var entities = new EntityFrameSnapshot[EntityCount];
        for (var index = 0; index < entities.Length; index++)
        {
            entities[index] = new EntityFrameSnapshot(
                index + 1,
                EntityFrameKind.Enemy,
                "benchmark_enemy",
                new Vector2(index * 8, 64),
                new Vector2(index % 3 - 1, 0),
                new RectI(index * 8, 64, 16, 24),
                IsActive: true,
                Health: 20,
                MaxHealth: 20,
                ItemStack.Empty,
                IsDamageFlashing: false,
                DamageType.Generic);
        }

        _entities = new ImmutableSnapshotList<EntityFrameSnapshot>(entities);
    }

    [Benchmark(Baseline = true)]
    public int QueryByIndex()
    {
        var checksum = 0;
        for (var index = 0; index < _entities.Count; index++)
        {
            checksum = unchecked(checksum * 397 + _entities[index].Id);
        }

        return checksum;
    }

    [Benchmark]
    public int QueryByConcreteEnumerator()
    {
        var checksum = 0;
        foreach (var entity in _entities)
        {
            checksum = unchecked(checksum * 397 + entity.Id);
        }

        return checksum;
    }
}

internal static class BenchmarkContent
{
    private static readonly Lazy<GameContentDatabase> Content = new(LoadCore);

    public static GameContentDatabase Load()
    {
        return Content.Value;
    }

    private static GameContentDatabase LoadCore()
    {
        var result = new GameContentLoader().LoadWithMods(Program.FindGameDataRoot(), modsRoot: null);
        if (result.Report.HasErrors)
        {
            throw new InvalidOperationException(
                "Benchmark content is invalid: " +
                string.Join(" | ", result.Report.Issues.Select(issue => issue.Message)));
        }

        return result.Database;
    }
}
