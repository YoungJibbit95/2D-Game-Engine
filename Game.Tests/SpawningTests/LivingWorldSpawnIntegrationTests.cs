using Game.Core;
using Game.Core.Entities;
using Game.Core.Sessions;
using Game.Core.Settings;
using Game.Core.World;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.SpawningTests;

[Collection(EntityReliabilityCollection.Name)]
public sealed class LivingWorldSpawnIntegrationTests : IDisposable
{
    private const int SoakTicks = 18_000;
    private const int SoakSegmentTicks = 1_500;

    private readonly string _saveRoot = Path.Combine(
        Path.GetTempPath(),
        "yjse-living-world-spawn-tests",
        Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public LivingWorldSpawnIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PlayingSession_PopulatesLoadedOffscreenRingWithFriendlyAndHostileActors()
    {
        var projectRoot = FindProjectRoot();
        var settings = GameSettings.CreateDefault() with
        {
            Video = GameSettings.CreateDefault().Video with
            {
                Width = 1920,
                Height = 1080
            },
            Gameplay = GameSettings.CreateDefault().Gameplay with
            {
                CameraZoom = 2f,
                MaxActiveEnemies = 32,
                EnemySpawnRateMultiplier = 1f,
                SpawnMinimumDistancePixels = 520f,
                SpawnMaximumDistancePixels = 1500f,
                SpawnOutsideViewportOnly = true
            },
            World = GameSettings.CreateDefault().World with
            {
                InfiniteHorizontalGeneration = true,
                WorldProfileId = "large",
                PreloadFullVerticalSlice = true
            }
        };
        using var session = new GameSessionBootstrapper().LoadOrCreate(
            new GameSessionBootstrapRequest(
                projectRoot,
                _saveRoot,
                Seed: 7_331,
                WorldName: "Living World Spawn Integration",
                settings)).Session;

        var visibleHalfWidth = (int)MathF.Ceiling(
            settings.Video.Width / settings.Gameplay.CameraZoom /
            (GameConstants.TileSize * 2f));
        var visibleHalfHeight = (int)MathF.Ceiling(
            settings.Video.Height / settings.Gameplay.CameraZoom /
            (GameConstants.TileSize * 2f));
        session.Simulation.ConfigureOptions(session.Simulation.Options with
        {
            MaxActiveEnemies = settings.Gameplay.MaxActiveEnemies,
            EnemySpawnRateMultiplier = settings.Gameplay.EnemySpawnRateMultiplier,
            SpawnMinimumDistanceTiles = (int)MathF.Ceiling(
                settings.Gameplay.SpawnMinimumDistancePixels / GameConstants.TileSize),
            SpawnMaximumDistanceTiles = (int)MathF.Ceiling(
                settings.Gameplay.SpawnMaximumDistancePixels / GameConstants.TileSize),
            SpawnVisibleHalfWidthTiles = visibleHalfWidth,
            SpawnVisibleHalfHeightTiles = visibleHalfHeight,
            SpawnOutsideViewportOnly = settings.Gameplay.SpawnOutsideViewportOnly
        });

        var initialCenter = CoordinateUtils.WorldToTile(
            session.Player.Body.Center.X,
            session.Player.Body.Center.Y);
        var initialVisible = RectI.FromInclusiveTileBounds(
            initialCenter.X - visibleHalfWidth,
            initialCenter.Y - visibleHalfHeight,
            initialCenter.X + visibleHalfWidth,
            initialCenter.Y + visibleHalfHeight);
        var totalAttempts = 0;
        var totalSpawned = 0;
        var spawnedOutsideView = 0;
        var observedVisibleIngress = false;
        var observedActorIds = new HashSet<int>();

        foreach (var rule in session.Content.SpawnRules.Definitions)
        {
            var definition = session.Content.Entities.GetById(rule.EntityId);
            Assert.True(
                definition.Ai is not null || !string.IsNullOrWhiteSpace(definition.AiBehavior),
                $"Spawn rule '{rule.Id}' references inert entity '{definition.Id}'.");
        }

        for (var tick = 0; tick < 60 * 90; tick++)
        {
            var result = session.Simulation.Tick(PlayerCommand.None, 1f / 60f);
            totalAttempts += result.Spawning.Attempts;
            totalSpawned += result.Spawning.Spawned;
            for (var index = 0; index < session.Entities.Entities.Count; index++)
            {
                if (session.Entities.Entities[index] is not EnemyEntity actor)
                {
                    continue;
                }

                var actorTile = CoordinateUtils.WorldToTile(actor.Body.Center.X, actor.Body.Center.Y);
                observedVisibleIngress |= initialVisible.Contains(actorTile.X, actorTile.Y);
                if (!observedActorIds.Add(actor.Id))
                {
                    continue;
                }

                Assert.True(
                    session.World.TryGetChunk(CoordinateUtils.TileToChunk(actorTile), out _),
                    $"Actor '{actor.DefinitionId}' spawned in an unloaded chunk at {actorTile}.");
                if (!initialVisible.Contains(actorTile.X, actorTile.Y))
                {
                    spawnedOutsideView++;
                }
            }
        }

        var actors = session.Entities.Entities.OfType<EnemyEntity>().ToArray();
        Assert.True(totalAttempts > 0, "The real session never attempted population maintenance.");
        Assert.True(totalSpawned > 0, $"Spawn attempts={totalAttempts}, but no actor was accepted.");
        Assert.True(spawnedOutsideView > 0, "No accepted actor originated outside the initial viewport.");
        Assert.Contains(actors, actor => actor.Faction == EntityFaction.Friendly);
        Assert.Contains(actors, actor => actor.Faction == EntityFaction.Hostile);
        Assert.True(observedVisibleIngress, "No offscreen actor reached the visible activity area.");
    }

    [Fact]
    public void FiveMinuteStreamingSoak_IsDeterministicAndMaintainsLivingPopulations()
    {
        var first = RunStreamingSoak("first");
        var second = RunStreamingSoak("second");
        _output.WriteLine(
            $"18,000 ticks x2: hash=0x{first.TraceHash:X16}, spawned={first.TotalSpawned}, " +
            $"despawned={first.TotalDespawned}, maxPopulation={first.MaximumPopulation}, " +
            $"maxZeroRun={first.LongestZeroPopulationRun}, maxLoadedChunks={first.MaximumLoadedChunks}");
        _output.WriteLine($"Biomes: {string.Join(", ", first.ObservedBiomeIds)}");
        _output.WriteLine($"Encounters: {string.Join(", ", first.ObservedEncounterIds)}");
        _output.WriteLine($"Definitions: {string.Join(", ", first.ObservedDefinitionIds)}");
        _output.WriteLine($"Segment population peaks: {string.Join(", ", first.SegmentPopulationPeaks)}");

        Assert.Equal(first.TraceHash, second.TraceHash);
        Assert.Equal(first.ObservedBiomeIds, second.ObservedBiomeIds);
        Assert.Equal(first.ObservedEncounterIds, second.ObservedEncounterIds);
        Assert.Equal(first.ObservedDefinitionIds, second.ObservedDefinitionIds);
        Assert.Equal(first.SegmentPopulationPeaks, second.SegmentPopulationPeaks);
        Assert.Equal(SoakTicks, first.TickCount);
        Assert.True(first.TotalSpawned > 0);
        Assert.True(first.TotalDespawned > 0);
        Assert.True(first.SawFriendly);
        Assert.True(first.SawHostile);
        Assert.True(first.SawNegativeX);
        Assert.True(first.SawPositiveX);
        Assert.True(first.SurfaceTicks > 0);
        Assert.True(first.CaveTicks > 0);
        Assert.True(first.DayTicks > 0);
        Assert.True(first.NightTicks > 0);
        Assert.True(first.MaximumPopulation <= 32);
        Assert.True(
            first.LongestZeroPopulationRun <= 600,
            $"Population remained at zero for {first.LongestZeroPopulationRun} ticks.");
        Assert.True(first.MaximumLoadedChunks <= 256);
        Assert.True(first.ObservedBiomeIds.Length >= 3);
        Assert.True(first.ObservedEncounterIds.Length >= 2);
        Assert.True(first.ObservedDefinitionIds.Length >= 4);
        Assert.All(first.SegmentPopulationPeaks, peak => Assert.True(peak > 0));
    }

    private StreamingSoakResult RunStreamingSoak(string runId)
    {
        const int seed = 7_331;
        var settings = GameSettings.CreateDefault() with
        {
            Gameplay = GameSettings.CreateDefault().Gameplay with
            {
                MaxActiveEnemies = 32,
                EnemySpawnRateMultiplier = 6f,
                SpawnMinimumDistancePixels = 18 * GameConstants.TileSize,
                SpawnMaximumDistancePixels = 46 * GameConstants.TileSize,
                SpawnOutsideViewportOnly = true
            },
            World = GameSettings.CreateDefault().World with
            {
                InfiniteHorizontalGeneration = true,
                WorldProfileId = "large",
                PreloadFullVerticalSlice = true
            }
        };
        var bootstrap = new GameSessionBootstrapper().LoadOrCreate(
            new GameSessionBootstrapRequest(
                FindProjectRoot(),
                Path.Combine(_saveRoot, runId),
                seed,
                $"Entity Reliability Soak {runId}",
                settings)
            {
                LoadExistingSave = false
            });
        using var session = bootstrap.Session;
        var generator = Assert.IsType<Game.Core.World.Generation.InfiniteWorldChunkGenerator>(
            session.InfiniteChunkGenerator);
        var profile = Assert.IsType<Game.Core.World.Generation.WorldGenerationProfile>(
            session.WorldGenerationProfile);
        var surfaceHeight = generator.CreateSurfaceHeightResolver(profile, seed);
        var waypoints = CreateWaypoints(session);
        Assert.Equal(SoakTicks / SoakSegmentTicks, waypoints.Length);
        session.Simulation.ConfigureOptions(session.Simulation.Options with
        {
            AutoPickupItems = false,
            MaxActiveEnemies = 32,
            EnemySpawnRateMultiplier = 6f,
            SpawnMinimumDistanceTiles = 18,
            SpawnMaximumDistanceTiles = 46,
            SpawnVisibleHalfWidthTiles = 16,
            SpawnVisibleHalfHeightTiles = 10,
            SpawnOutsideViewportOnly = true
        });

        var observedActors = new HashSet<int>();
        var observedBiomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var observedEncounters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var observedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occupiedChunks = new HashSet<ChunkPos>();
        var unloadCandidates = new List<ChunkPos>();
        var segmentPopulationPeaks = new int[waypoints.Length];
        var totalSpawned = 0;
        var totalDespawned = 0;
        var maximumPopulation = 0;
        var maximumLoadedChunks = session.World.Chunks.Count;
        var currentZeroRun = 0;
        var longestZeroRun = 0;
        var surfaceTicks = 0;
        var caveTicks = 0;
        var dayTicks = 0;
        var nightTicks = 0;
        var sawFriendly = false;
        var sawHostile = false;
        var sawNegativeX = false;
        var sawPositiveX = false;
        var traceHash = 14695981039346656037UL;

        for (var tick = 0; tick < SoakTicks; tick++)
        {
            var segment = tick / SoakSegmentTicks;
            var segmentTick = tick % SoakSegmentTicks;
            var waypoint = waypoints[segment];
            if (segmentTick == 0)
            {
                var normalizedTime = (segment & 1) == 0 ? 0.5 : 0.0;
                session.WorldTime.RestoreState(
                    segment + 1,
                    normalizedTime * session.WorldTime.DayLengthSeconds);
            }

            var travel = segmentTick / (double)(SoakSegmentTicks - 1);
            var tileX = waypoint.TileX + (int)Math.Round((travel * 2d - 1d) * waypoint.DriftRadiusTiles);
            var tileY = waypoint.IsSurface ? surfaceHeight(tileX) - 2 : waypoint.TileY;
            sawNegativeX |= tileX < 0;
            sawPositiveX |= tileX > 0;
            session.Player.Body.Position = new Vector2(
                tileX * GameConstants.TileSize,
                tileY * GameConstants.TileSize);
            session.Player.Body.Velocity = Vector2.Zero;
            session.Player.HealthComponent.RestoreFull();

            if (segmentTick % 30 == 0)
            {
                StreamAround(
                    session,
                    generator,
                    profile,
                    tileX,
                    occupiedChunks,
                    unloadCandidates);
                maximumLoadedChunks = Math.Max(maximumLoadedChunks, session.World.Chunks.Count);
            }

            var tickResult = session.Simulation.Tick(PlayerCommand.None, 1f / 60f);
            totalSpawned += tickResult.Spawning.Spawned;
            totalDespawned += tickResult.Spawning.Despawned;
            var livingWorld = tickResult.Snapshot.LivingWorld;
            if (segmentTick == 0)
            {
                _output.WriteLine(
                    $"{runId} segment {segment}: tile=({tileX},{tileY}), surface={waypoint.IsSurface}, " +
                    $"biome={livingWorld.BiomeId}, layer={livingWorld.BiomeLayerId}");
            }

            observedBiomes.Add(livingWorld.BiomeId);
            if (livingWorld.IsUnderground)
            {
                caveTicks++;
            }
            else
            {
                surfaceTicks++;
            }

            if (tickResult.Snapshot.WorldTime.IsNight)
            {
                nightTicks++;
            }
            else
            {
                dayTicks++;
            }

            var population = 0;
            for (var entityIndex = 0; entityIndex < session.Entities.Entities.Count; entityIndex++)
            {
                if (session.Entities.Entities[entityIndex] is not EnemyEntity { IsActive: true } actor)
                {
                    continue;
                }

                population++;
                sawFriendly |= actor.Faction == EntityFaction.Friendly;
                sawHostile |= actor.Faction == EntityFaction.Hostile;
                observedDefinitions.Add(actor.DefinitionId);
                if (!string.IsNullOrWhiteSpace(actor.SpawnEncounterId))
                {
                    observedEncounters.Add(actor.SpawnEncounterId);
                }

                if (observedActors.Add(actor.Id))
                {
                    AssertActorContract(session, actor, requireAiUpdate: false);
                }
            }

            if (tick % 30 == 0)
            {
                for (var entityIndex = 0; entityIndex < session.Entities.Entities.Count; entityIndex++)
                {
                    if (session.Entities.Entities[entityIndex] is EnemyEntity { IsActive: true } actor)
                    {
                        AssertActorContract(session, actor, requireAiUpdate: tickResult.Spawning.Spawned == 0);
                    }
                }
            }

            maximumPopulation = Math.Max(maximumPopulation, population);
            segmentPopulationPeaks[segment] = Math.Max(segmentPopulationPeaks[segment], population);
            if (population == 0)
            {
                currentZeroRun++;
                longestZeroRun = Math.Max(longestZeroRun, currentZeroRun);
            }
            else
            {
                currentZeroRun = 0;
            }

            if (population > 32)
            {
                throw new InvalidOperationException($"Population cap exceeded at tick {tick}: {population}.");
            }

            traceHash = Hash(traceHash, tick);
            traceHash = Hash(traceHash, tileX);
            traceHash = Hash(traceHash, tileY);
            traceHash = Hash(traceHash, population);
            traceHash = Hash(traceHash, tickResult.Spawning.Spawned);
            traceHash = Hash(traceHash, tickResult.Spawning.Despawned);
            traceHash = Hash(traceHash, livingWorld.BiomeId);
            for (var entityIndex = 0; entityIndex < session.Entities.Entities.Count; entityIndex++)
            {
                if (session.Entities.Entities[entityIndex] is not EnemyEntity { IsActive: true } actor)
                {
                    continue;
                }

                traceHash = Hash(traceHash, actor.Id);
                traceHash = Hash(traceHash, actor.DefinitionId);
                traceHash = Hash(traceHash, BitConverter.SingleToInt32Bits(actor.Body.Position.X));
                traceHash = Hash(traceHash, BitConverter.SingleToInt32Bits(actor.Body.Position.Y));
                traceHash = Hash(traceHash, actor.SpawnRuleId);
                traceHash = Hash(traceHash, actor.SpawnEncounterId);
            }
        }

        return new StreamingSoakResult(
            SoakTicks,
            traceHash,
            totalSpawned,
            totalDespawned,
            maximumPopulation,
            longestZeroRun,
            maximumLoadedChunks,
            sawFriendly,
            sawHostile,
            sawNegativeX,
            sawPositiveX,
            surfaceTicks,
            caveTicks,
            dayTicks,
            nightTicks,
            observedBiomes.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            observedEncounters.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            observedDefinitions.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            segmentPopulationPeaks);
    }

    private static SoakWaypoint[] CreateWaypoints(LoadedGameSession session)
    {
        var livingWorld = session.Simulation.LivingWorld;
        var regionWidth = livingWorld.Profile.RegionWidthTiles;
        var surface = new List<SoakWaypoint>();
        var caves = new List<SoakWaypoint>();
        for (var regionIndex = -9; regionIndex <= 9; regionIndex++)
        {
            var centerX = checked(regionIndex * regionWidth + regionWidth / 2);
            var region = livingWorld.ResolveRegion(centerX);
            if (regionIndex % 3 == 0)
            {
                surface.Add(new SoakWaypoint(centerX, 0, IsSurface: true, DriftRadiusTiles: 24));
            }

            for (var caveIndex = 0; caveIndex < region.Caves.Count; caveIndex++)
            {
                var cave = region.Caves[caveIndex];
                caves.Add(new SoakWaypoint(
                    checked((int)cave.CenterTileX),
                    cave.CenterTileY,
                    IsSurface: false,
                    DriftRadiusTiles: Math.Clamp(cave.RadiusX / 3, 1, 6)));
            }
        }

        var selectedSurface = surface
            .OrderBy(point => point.TileX)
            .Take(6)
            .ToArray();
        var selectedCaves = caves
            .OrderBy(point => point.TileX)
            .ThenBy(point => point.TileY)
            .GroupBy(point => livingWorld.ResolveRegion(point.TileX).Caves
                .First(cave => cave.CenterTileX == point.TileX && cave.CenterTileY == point.TileY)
                .ProfileId,
                StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Take(2))
            .Concat(caves.OrderByDescending(point => point.TileX))
            .Distinct()
            .Take(6)
            .ToArray();
        if (selectedSurface.Length < 6 || selectedCaves.Length < 6)
        {
            throw new InvalidOperationException("The regional profile did not provide enough soak waypoints.");
        }

        var result = new SoakWaypoint[12];
        for (var index = 0; index < 6; index++)
        {
            result[index * 2] = selectedSurface[index];
            result[index * 2 + 1] = selectedCaves[5 - index];
        }

        return result;
    }

    private static void StreamAround(
        LoadedGameSession session,
        Game.Core.World.Generation.InfiniteWorldChunkGenerator generator,
        Game.Core.World.Generation.WorldGenerationProfile profile,
        int centerTileX,
        HashSet<ChunkPos> occupiedChunks,
        List<ChunkPos> unloadCandidates)
    {
        var centerChunkX = CoordinateUtils.TileToChunk(centerTileX, 0).X;
        var maximumChunkY = CoordinateUtils.TileToChunk(0, profile.HeightTiles - 1).Y;
        for (var chunkX = centerChunkX - 3; chunkX <= centerChunkX + 3; chunkX++)
        {
            for (var chunkY = 0; chunkY <= maximumChunkY; chunkY++)
            {
                generator.EnsureChunk(session.World, profile, new ChunkPos(chunkX, chunkY));
            }
        }

        occupiedChunks.Clear();
        occupiedChunks.Add(CoordinateUtils.TileToChunk(CoordinateUtils.WorldToTile(
            session.Player.Body.Center.X,
            session.Player.Body.Center.Y)));
        for (var entityIndex = 0; entityIndex < session.Entities.Entities.Count; entityIndex++)
        {
            if (session.Entities.Entities[entityIndex].IsActive)
            {
                var bounds = session.Entities.Entities[entityIndex].Bounds;
                var minimumTile = CoordinateUtils.WorldToTile(bounds.Left, bounds.Top);
                var maximumTile = CoordinateUtils.WorldToTile(
                    bounds.Right - 0.01f,
                    bounds.Bottom - 0.01f);
                var minimumChunk = CoordinateUtils.TileToChunk(minimumTile);
                var maximumChunk = CoordinateUtils.TileToChunk(maximumTile);
                for (var chunkY = minimumChunk.Y; chunkY <= maximumChunk.Y; chunkY++)
                {
                    for (var chunkX = minimumChunk.X; chunkX <= maximumChunk.X; chunkX++)
                    {
                        occupiedChunks.Add(new ChunkPos(chunkX, chunkY));
                    }
                }
            }
        }

        unloadCandidates.Clear();
        foreach (var chunk in session.World.Chunks.Keys)
        {
            if (Math.Abs((long)chunk.X - centerChunkX) > 4 && !occupiedChunks.Contains(chunk))
            {
                unloadCandidates.Add(chunk);
            }
        }

        for (var index = 0; index < unloadCandidates.Count; index++)
        {
            session.World.UnloadChunk(unloadCandidates[index], requireClean: false);
        }
    }

    private static void AssertActorContract(
        LoadedGameSession session,
        EnemyEntity actor,
        bool requireAiUpdate)
    {
        var definition = session.Content.Entities.GetById(actor.DefinitionId);
        if (definition.Ai is null && string.IsNullOrWhiteSpace(definition.AiBehavior))
        {
            throw new InvalidOperationException($"Actor '{actor.DefinitionId}' has no AI definition.");
        }

        if (!Enum.IsDefined(actor.AiState) || requireAiUpdate && actor.AiTelemetry.UpdateCount == 0)
        {
            throw new InvalidOperationException($"Actor '{actor.DefinitionId}' has no live AI state.");
        }

        var minTile = CoordinateUtils.WorldToTile(actor.Body.Position.X, actor.Body.Position.Y);
        var maxTile = CoordinateUtils.WorldToTile(
            actor.Body.Position.X + actor.Body.Size.X - 0.01f,
            actor.Body.Position.Y + actor.Body.Size.Y - 0.01f);
        for (var tileY = minTile.Y; tileY <= maxTile.Y; tileY++)
        {
            for (var tileX = minTile.X; tileX <= maxTile.X; tileX++)
            {
                if (!session.World.TryGetTile(tileX, tileY, out var tile))
                {
                    throw new InvalidOperationException(
                        $"Actor '{actor.DefinitionId}' entered unloaded tile ({tileX},{tileY}).");
                }

                if (tile.IsSolid)
                {
                    throw new InvalidOperationException(
                        $"Actor '{actor.DefinitionId}' intersects solid tile ({tileX},{tileY}); " +
                        $"position={actor.Body.Position}, size={actor.Body.Size}, velocity={actor.Body.Velocity}, " +
                        $"onGround={actor.Body.OnGround}, rule='{actor.SpawnRuleId}', encounter='{actor.SpawnEncounterId}'.");
                }
            }
        }
    }

    private static ulong Hash(ulong hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            return hash * 1099511628211UL;
        }
    }

    private static ulong Hash(ulong hash, string? value)
    {
        if (value is null)
        {
            return Hash(hash, -1);
        }

        for (var index = 0; index < value.Length; index++)
        {
            hash = Hash(hash, value[index]);
        }

        return Hash(hash, value.Length);
    }

    private readonly record struct SoakWaypoint(
        int TileX,
        int TileY,
        bool IsSurface,
        int DriftRadiusTiles);

    private sealed record StreamingSoakResult(
        int TickCount,
        ulong TraceHash,
        int TotalSpawned,
        int TotalDespawned,
        int MaximumPopulation,
        int LongestZeroPopulationRun,
        int MaximumLoadedChunks,
        bool SawFriendly,
        bool SawHostile,
        bool SawNegativeX,
        bool SawPositiveX,
        int SurfaceTicks,
        int CaveTicks,
        int DayTicks,
        int NightTicks,
        string[] ObservedBiomeIds,
        string[] ObservedEncounterIds,
        string[] ObservedDefinitionIds,
        int[] SegmentPopulationPeaks);

    public void Dispose()
    {
        if (Directory.Exists(_saveRoot))
        {
            Directory.Delete(_saveRoot, recursive: true);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "yjse.game.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Game.Data")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the YjsE project root.");
    }
}
