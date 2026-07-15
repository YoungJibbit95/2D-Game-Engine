using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Runtime;
using Game.Core.Sessions;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Game.Benchmarks;

public static class Program
{
    private const float FixedDeltaSeconds = 1f / 60f;
    private const int SimulationWorldWidth = 128;
    private const int SimulationWorldHeight = 64;
    private const int SimulationSeed = 424242;
    private const int DeterminismCheckpointInterval = 60;

    private static int _checksum;

    public static int Main(string[] args)
    {
        try
        {
            if (BenchmarkDotNetHost.TryRun(args, out var benchmarkExitCode))
            {
                return benchmarkExitCode;
            }

            _checksum = 0;
            var options = BenchmarkOptions.Parse(args);
            var dataRoot = options.DataRoot ?? FindGameDataRoot();
            var loader = new GameContentLoader();
            var initialLoad = loader.LoadWithMods(dataRoot, modsRoot: null);
            if (initialLoad.Report.HasErrors)
            {
                foreach (var issue in initialLoad.Report.Issues)
                {
                    Console.Error.WriteLine($"{issue.Severity}: {issue.ContentKind}/{issue.ContentId}: {issue.Message}");
                }

                return 2;
            }

            var measurements = new List<BenchmarkMeasurement>
            {
                MeasureEventDispatch(options),
                Measure(
                    "Content.Load",
                    options.ContentWarmup,
                    options.ContentIterations,
                    () =>
                    {
                        var result = loader.LoadWithMods(dataRoot, modsRoot: null);
                        Consume(result.Database.SpriteAssets.Definitions.Count);
                    }),
                Measure(
                    "World.Generate.Simple.256x128",
                    options.WorldgenWarmup,
                    options.WorldgenIterations,
                    () =>
                    {
                        var world = new SimpleWorldGenerator().Generate(256, 128, 1337);
                        Consume(world.Chunks.Count);
                    }),
                Measure(
                    "World.Streaming.InitialWindow",
                    options.WorldgenWarmup,
                    options.WorldgenIterations,
                    RunInitialStreamingWindow)
            };

            using var session = CreateRepresentativeSession(initialLoad.Database);
            var quickProfile = options.TickIterations == 1_200;
            measurements.Add(Measure(
                "Simulation.FixedTick",
                options.TickWarmup,
                options.TickIterations,
                () =>
                {
                    var simulation = session.Simulation;
                    var result = simulation.Tick(CommandForTick(simulation.TickNumber), FixedDeltaSeconds);
                    Consume(
                        result.PickedUpItems +
                        result.WorldSimulation.LiquidRegionsProcessed +
                        result.Snapshot.Entities.Count +
                        result.Snapshot.Hud.TotalInventoryItems);
                }));

            var phaseTelemetry = CaptureSimulationPhaseTelemetry(
                session,
                options.TickWarmup,
                options.TickIterations);
            measurements.Add(Measure(
                "Simulation.StateHash",
                quickProfile ? 1 : 4,
                quickProfile ? 12 : 100,
                () => Consume(ComputeStateHash(session))));

            var determinism = MeasureDeterministicReplay(initialLoad.Database, quickProfile);
            measurements.Add(determinism.Measurement);

            var report = new BenchmarkReport(
                SchemaVersion: 3,
                ScenarioVersion: "yjse-epic1-harness-v3",
                Profile: quickProfile ? "quick-smoke" : "calibration-sample",
                CapturedAtUtc: DateTimeOffset.UtcNow,
                Configuration: BuildConfiguration(),
                RepositoryRevision: FindRepositoryRevision(dataRoot),
                RepositoryState: Environment.GetEnvironmentVariable("YJSE_BENCHMARK_REPOSITORY_STATE") ?? "unknown-not-inspected",
                Scenario: new BenchmarkScenario(
                    ContentContract: "base-content-without-mods",
                    WorldWidth: 256,
                    WorldHeight: 128,
                    WorldSeed: 1337,
                    SimulationWorldWidth,
                    SimulationWorldHeight,
                    SimulationSeed,
                    FixedDeltaSeconds),
                Environment: new BenchmarkEnvironment(
                    RuntimeInformation.OSDescription,
                    RuntimeInformation.ProcessArchitecture.ToString(),
                    RuntimeInformation.FrameworkDescription,
                    Environment.Version.ToString(),
                    ProcessorDescription(),
                    Environment.ProcessorCount,
                    GCSettings()),
                DataRoot: Path.GetFullPath(dataRoot),
                Measurements: measurements,
                PhaseTelemetry: phaseTelemetry,
                Checksum: _checksum,
                Determinism: determinism.Verification);

            Print(report);
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                var outputPath = Path.GetFullPath(options.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(
                    outputPath,
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
                Console.WriteLine($"Report: {outputPath}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    internal static LoadedGameSession CreateRepresentativeSession(GameContentDatabase content)
    {
        var world = new SimpleWorldGenerator().Generate(SimulationWorldWidth, SimulationWorldHeight, SimulationSeed);
        var spawn = world.Metadata.SpawnTile;
        var collision = new TileCollisionResolver();
        var player = new PlayerEntity(
            new Vector2(spawn.X * Game.Core.GameConstants.TileSize, Math.Max(0, spawn.Y - 2) * Game.Core.GameConstants.TileSize),
            collision);
        var inventory = new PlayerInventory(content.Items);
        SeedInventory(content, inventory);

        var entities = new EntityManager(spatialCellSize: 64);
        SeedEntities(content, entities, player.Body.Position, collision);

        var farmPlots = new FarmPlotManager();
        SeedFarmPlots(content, farmPlots, spawn);

        var equipment = new EquipmentLoadout();
        foreach (var definition in content.Items.Definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal))
        {
            equipment.TryEquipFirstAvailable(new ItemStack(definition.Id, 1), content.Items);
        }

        var events = new GameEventBus();
        var time = new WorldTime();
        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            entities,
            time,
            events,
            combat: new CombatSystem(new LootRoller(new Random(11)), collision),
            spawnScheduler: new SpawnScheduler(new Random(12)),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            farmPlots: farmPlots,
            equipmentLoadout: equipment);

        world.ClearAllDirtyFlags();
        return new LoadedGameSession(
            content,
            world,
            player,
            inventory,
            entities,
            events,
            time,
            simulation,
            farmPlots: farmPlots,
            equipmentLoadout: equipment);
    }

    private static void SeedInventory(GameContentDatabase content, PlayerInventory inventory)
    {
        var definitions = content.Items.Definitions
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .Take(18)
            .ToArray();
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var count = Math.Min(definition.MaxStack, 1 + ((index * 7) % 31));
            if (!inventory.AddItem(new ItemStack(definition.Id, count)))
            {
                throw new InvalidOperationException($"Could not seed benchmark inventory with '{definition.Id}'.");
            }
        }

        for (var index = 0; index < inventory.Hotbar.Slots.Count; index += 3)
        {
            if (!inventory.Hotbar.Slots[index].IsEmpty)
            {
                inventory.Hotbar.SetFavorite(index, true);
            }
        }
    }

    private static void SeedEntities(
        GameContentDatabase content,
        EntityManager entities,
        Vector2 playerPosition,
        TileCollisionResolver collision)
    {
        var enemyDefinitions = content.Entities.Definitions
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var entityFactory = new EntityFactory(collision);
        for (var index = 0; index < 12 && enemyDefinitions.Length > 0; index++)
        {
            var offsetX = (index - 6) * 38;
            var spawnPosition = playerPosition + new Vector2(offsetX, -24 - (index % 3) * 8);
            entities.Add(entityFactory.CreateEnemy(enemyDefinitions[index % enemyDefinitions.Length], spawnPosition));
        }

        var itemDefinitions = content.Items.Definitions
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .Take(12)
            .ToArray();
        for (var index = 0; index < itemDefinitions.Length; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            var distance = 140 + (index / 2) * 28;
            var position = playerPosition + new Vector2(side * distance, -36 - (index % 3) * 6);
            entities.Add(new DroppedItemEntity(new ItemStack(itemDefinitions[index].Id, 1 + index % 4), position, collision));
        }
    }

    private static void SeedFarmPlots(GameContentDatabase content, FarmPlotManager farmPlots, TilePos spawn)
    {
        var crop = content.Crops.Definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault();
        for (var index = 0; index < 12; index++)
        {
            var plot = farmPlots.GetOrCreatePlot(new TilePos(spawn.X - 6 + index, Math.Max(0, spawn.Y - 1)));
            plot.IsTilled = true;
            plot.IsWatered = index % 3 != 0;
            if (crop is not null && index % 2 == 0)
            {
                plot.Crop = new CropInstance(crop.Id, plantedDay: 1, Math.Max(1, crop.TotalGrowthDays - index % 3));
            }
        }
    }

    internal static PlayerCommand CommandForTick(long tickNumber)
    {
        var phase = (tickNumber / 180) % 4;
        var moveAxis = phase switch
        {
            0 => 1f,
            1 => -0.35f,
            2 => -1f,
            _ => 0.35f
        };
        var wantsJump = tickNumber % 75 == 12;
        return new PlayerCommand(moveAxis, wantsJump);
    }

    private static ulong ComputeStateHash(LoadedGameSession session)
    {
        return SimulationStateHasher.Compute(
            session.World,
            session.Player,
            session.Inventory,
            session.Entities,
            session.WorldTime,
            session.FarmPlots,
            session.EquipmentLoadout,
            session.RandomStreams);
    }

    private static DeterminismBenchmarkResult MeasureDeterministicReplay(
        GameContentDatabase content,
        bool quickProfile)
    {
        var ticksPerReplay = quickProfile ? 240 : 1_200;
        BenchmarkDeterminismVerification? verification = null;
        var measurement = Measure(
            "Simulation.DeterministicReplay",
            quickProfile ? 0 : 1,
            quickProfile ? 1 : 3,
            () => verification = RunDeterministicReplay(content, ticksPerReplay));

        return new DeterminismBenchmarkResult(
            measurement,
            verification ?? throw new InvalidOperationException("Determinism replay did not execute."));
    }

    private static BenchmarkPhaseTelemetry CaptureSimulationPhaseTelemetry(
        LoadedGameSession session,
        int warmupTicks,
        int measuredTicks)
    {
        var simulation = session.Simulation;
        var originalOptions = simulation.Options;
        simulation.ConfigureOptions(originalOptions with { EnablePhaseTelemetry = true });
        try
        {
            for (var index = 0; index < warmupTicks; index++)
            {
                var result = simulation.Tick(CommandForTick(simulation.TickNumber), FixedDeltaSeconds);
                Consume(result.Snapshot.Entities.Count);
            }

            simulation.PhaseTelemetry.Reset();
            for (var index = 0; index < measuredTicks; index++)
            {
                var result = simulation.Tick(CommandForTick(simulation.TickNumber), FixedDeltaSeconds);
                Consume(result.Snapshot.Entities.Count);
            }

            var snapshot = simulation.PhaseTelemetry.CaptureSnapshot();
            var measurements = new BenchmarkPhaseMeasurement[snapshot.Measurements.Count];
            for (var index = 0; index < measurements.Length; index++)
            {
                var measurement = snapshot.Measurements[index];
                measurements[index] = new BenchmarkPhaseMeasurement(
                    measurement.Phase.ToString(),
                    measurement.Samples,
                    measurement.AverageMilliseconds,
                    measurement.AverageAllocatedBytes,
                    measurement.LastAllocatedBytes);
            }

            return new BenchmarkPhaseTelemetry(warmupTicks, measuredTicks, measurements);
        }
        finally
        {
            simulation.ConfigureOptions(originalOptions);
            simulation.PhaseTelemetry.Reset();
        }
    }

    private static BenchmarkDeterminismVerification RunDeterministicReplay(
        GameContentDatabase content,
        int ticksPerReplay)
    {
        using var first = CreateRepresentativeSession(content);
        using var second = CreateRepresentativeSession(content);
        ulong firstHash = 0;
        ulong secondHash = 0;

        for (var tick = 0; tick < ticksPerReplay; tick++)
        {
            var command = CommandForTick(tick);
            first.Simulation.Tick(command, FixedDeltaSeconds);
            second.Simulation.Tick(command, FixedDeltaSeconds);

            if ((tick + 1) % DeterminismCheckpointInterval != 0 && tick + 1 != ticksPerReplay)
            {
                continue;
            }

            firstHash = ComputeStateHash(first);
            secondHash = ComputeStateHash(second);
            if (firstHash != secondHash)
            {
                throw new InvalidOperationException(
                    $"Representative session replay diverged at tick {tick + 1}: " +
                    $"{FormatHash(firstHash)} != {FormatHash(secondHash)}.");
            }
        }

        Consume(firstHash);
        Consume(secondHash);
        return new BenchmarkDeterminismVerification(
            Trace: "representative-session-command-trace-v1",
            TicksPerReplay: ticksPerReplay,
            CheckpointInterval: DeterminismCheckpointInterval,
            FirstStateHash: FormatHash(firstHash),
            SecondStateHash: FormatHash(secondHash),
            Match: true);
    }

    private static string FormatHash(ulong hash)
    {
        return $"0x{hash:X16}";
    }

    private static BenchmarkMeasurement MeasureEventDispatch(BenchmarkOptions options)
    {
        var events = new GameEventBus();
        var gameEvent = new ChunkGeneratedEvent(new ChunkPos(-17, 3));
        var received = 0;
        events.Subscribe<ChunkGeneratedEvent>(_ => received++);
        var measurement = Measure(
            "Events.Publish.Typed",
            options.TickWarmup,
            options.TickIterations,
            () => events.Publish(gameEvent));
        Consume(received);
        return measurement;
    }

    private static void RunInitialStreamingWindow()
    {
        var profile = WorldGenerationProfile.Small;
        var world = new World(
            Game.Core.GameConstants.ChunkSize,
            profile.HeightTiles,
            WorldMetadata.CreateDefault(73013),
            isHorizontallyInfinite: true);
        var streaming = new ChunkStreamingService();
        var visible = new RectI(-32, profile.SurfaceBaseY - 12, 96, 32);
        var options = new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 1,
            MaxChunkOperationsPerUpdate = 8,
            MaxConcurrentLoadJobs = 4,
            MaxConcurrentSaveJobs = 1,
            MaxApplyQueueLength = 16
        };
        var deadline = Stopwatch.StartNew();
        ChunkStreamingUpdateResult result;
        do
        {
            result = streaming.Update(world, profile, visible, options: options);
            if (result.Plan.RequiredChunks.All(position => world.TryGetChunk(position, out _)))
            {
                break;
            }

            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException("Initial streaming benchmark did not settle within five seconds.");
            }

            Thread.Yield();
        }
        while (true);

        streaming.CancelPendingJobs();
        Consume(world.Chunks.Count + result.ApplyOperationsProcessed);
    }

    private static BenchmarkMeasurement Measure(string name, int warmup, int iterations, Action operation)
    {
        if (warmup < 0 || iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        for (var index = 0; index < warmup; index++)
        {
            operation();
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        var elapsed = new double[iterations];
        var allocations = new long[iterations];
        for (var index = 0; index < iterations; index++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var startedAt = Stopwatch.GetTimestamp();
            operation();
            elapsed[index] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            allocations[index] = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        }

        Array.Sort(elapsed);
        Array.Sort(allocations);
        return new BenchmarkMeasurement(
            name,
            warmup,
            iterations,
            elapsed.Average(),
            Percentile(elapsed, 0.50),
            Percentile(elapsed, 0.95),
            Percentile(elapsed, 0.99),
            elapsed[^1],
            allocations.Average(value => (double)value),
            (long)Percentile(allocations, 0.99),
            allocations[^1]);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static double Percentile(long[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static void Print(BenchmarkReport report)
    {
        Console.WriteLine(
            $"YjsE benchmark {report.ScenarioVersion}/{report.Profile} | {report.Configuration} | " +
            $"{report.RepositoryRevision} | {report.Environment.Framework} | {report.Environment.Os}");
        Console.WriteLine("Metric                                Avg ms   p95 ms   p99 ms   Avg alloc B   p99 alloc B");
        foreach (var measurement in report.Measurements)
        {
            Console.WriteLine(
                $"{measurement.Name,-36} " +
                $"{measurement.AverageMilliseconds,7:0.000} " +
                $"{measurement.P95Milliseconds,8:0.000} " +
                $"{measurement.P99Milliseconds,8:0.000} " +
                $"{measurement.AverageAllocatedBytes,13:0} " +
                $"{measurement.P99AllocatedBytes,13}");
        }

        Console.WriteLine("Fixed-tick phase telemetry                    Avg ms   Avg alloc B   Last alloc B");
        foreach (var phase in report.PhaseTelemetry.Measurements)
        {
            if (phase.Samples == 0)
            {
                continue;
            }

            Console.WriteLine(
                $"{phase.Phase,-46} " +
                $"{phase.AverageMilliseconds,7:0.000} " +
                $"{phase.AverageAllocatedBytes,13:0} " +
                $"{phase.LastAllocatedBytes,14}");
        }

        Console.WriteLine(
            $"Determinism                          {report.Determinism.TicksPerReplay,7} ticks | " +
            $"{report.Determinism.FirstStateHash} == {report.Determinism.SecondStateHash} | " +
            $"match={report.Determinism.Match}");
    }

    internal static string FindGameDataRoot()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in candidates)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "Game.Data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data. Use --data-root <path>.");
    }

    private static string GCSettings()
    {
        return $"server={System.Runtime.GCSettings.IsServerGC}; latency={System.Runtime.GCSettings.LatencyMode}";
    }

    private static string ProcessorDescription()
    {
        var environmentDescription = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        if (!string.IsNullOrWhiteSpace(environmentDescription))
        {
            return environmentDescription.Trim();
        }

        const string cpuInfoPath = "/proc/cpuinfo";
        if (File.Exists(cpuInfoPath))
        {
            var modelLine = File.ReadLines(cpuInfoPath)
                .FirstOrDefault(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
            var separator = modelLine?.IndexOf(':') ?? -1;
            if (separator >= 0)
            {
                return modelLine![(separator + 1)..].Trim();
            }
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static void Consume(int value)
    {
        _checksum = unchecked((_checksum * 397) ^ value);
    }

    private static void Consume(ulong value)
    {
        Consume(unchecked((int)value));
        Consume(unchecked((int)(value >> 32)));
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string FindRepositoryRevision(string dataRoot)
    {
        var environmentRevision = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(environmentRevision))
        {
            return environmentRevision.Trim();
        }

        var directory = new DirectoryInfo(Path.GetFullPath(dataRoot));
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath))
            {
                var headPath = Path.Combine(gitPath, "HEAD");
                if (!File.Exists(headPath))
                {
                    break;
                }

                var head = File.ReadAllText(headPath).Trim();
                const string refPrefix = "ref: ";
                if (!head.StartsWith(refPrefix, StringComparison.Ordinal))
                {
                    return head;
                }

                var referencePath = Path.Combine(gitPath, head[refPrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(referencePath) ? File.ReadAllText(referencePath).Trim() : head;
            }

            directory = directory.Parent;
        }

        return "unknown";
    }
}

public sealed record BenchmarkReport(
    int SchemaVersion,
    string ScenarioVersion,
    string Profile,
    DateTimeOffset CapturedAtUtc,
    string Configuration,
    string RepositoryRevision,
    string RepositoryState,
    BenchmarkScenario Scenario,
    BenchmarkEnvironment Environment,
    string DataRoot,
    IReadOnlyList<BenchmarkMeasurement> Measurements,
    BenchmarkPhaseTelemetry PhaseTelemetry,
    int Checksum,
    BenchmarkDeterminismVerification Determinism);

public sealed record BenchmarkPhaseTelemetry(
    int WarmupTicks,
    int MeasuredTicks,
    IReadOnlyList<BenchmarkPhaseMeasurement> Measurements);

public sealed record BenchmarkPhaseMeasurement(
    string Phase,
    long Samples,
    double AverageMilliseconds,
    double AverageAllocatedBytes,
    long LastAllocatedBytes);

public sealed record BenchmarkScenario(
    string ContentContract,
    int WorldWidth,
    int WorldHeight,
    int WorldSeed,
    int SimulationWorldWidth,
    int SimulationWorldHeight,
    int SimulationSeed,
    float FixedDeltaSeconds);

public sealed record BenchmarkEnvironment(
    string Os,
    string Architecture,
    string Framework,
    string RuntimeVersion,
    string Processor,
    int ProcessorCount,
    string GarbageCollector);

public sealed record BenchmarkMeasurement(
    string Name,
    int WarmupIterations,
    int Iterations,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds,
    double AverageAllocatedBytes,
    long P99AllocatedBytes,
    long MaximumAllocatedBytes);

public sealed record BenchmarkDeterminismVerification(
    string Trace,
    int TicksPerReplay,
    int CheckpointInterval,
    string FirstStateHash,
    string SecondStateHash,
    bool Match);

public sealed record DeterminismBenchmarkResult(
    BenchmarkMeasurement Measurement,
    BenchmarkDeterminismVerification Verification);

public sealed record BenchmarkOptions(
    string? DataRoot,
    string? OutputPath,
    int ContentWarmup,
    int ContentIterations,
    int WorldgenWarmup,
    int WorldgenIterations,
    int TickWarmup,
    int TickIterations)
{
    public static BenchmarkOptions Parse(string[] args)
    {
        string? dataRoot = null;
        string? output = null;
        var quick = args.Contains("--quick", StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--data-root", StringComparison.OrdinalIgnoreCase))
            {
                dataRoot = RequireValue(args, ref index, "--data-root");
            }
            else if (string.Equals(args[index], "--output", StringComparison.OrdinalIgnoreCase))
            {
                output = RequireValue(args, ref index, "--output");
            }
        }

        return quick
            ? new BenchmarkOptions(dataRoot, output, 1, 3, 1, 3, 120, 1_200)
            : new BenchmarkOptions(dataRoot, output, 2, 8, 2, 8, 1_000, 10_000);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[++index];
    }
}
