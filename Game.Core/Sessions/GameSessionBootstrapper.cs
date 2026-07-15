using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projects;
using Game.Core.Runtime;
using Game.Core.Saving;
using Game.Core.Settings;
using Game.Core.Startup;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.TileEntities;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Sessions;

public sealed class GameSessionBootstrapper
{
    private readonly GameProjectContentLoader _projectContentLoader;
    private readonly GameStartupInventoryService _startupInventory;
    private readonly GameLoadCoordinator _loadCoordinator;
    private readonly TileCollisionResolver _collisionResolver;
    private readonly Func<WorldSaveService> _worldSaveServiceFactory;

    public GameSessionBootstrapper()
        : this(
            new GameProjectContentLoader(),
            new GameStartupInventoryService(),
            new GameLoadCoordinator(),
            new TileCollisionResolver(),
            () => new WorldSaveService(WorldChunkStorageMode.RegionFiles))
    {
    }

    public GameSessionBootstrapper(
        GameProjectContentLoader projectContentLoader,
        GameStartupInventoryService startupInventory,
        GameLoadCoordinator loadCoordinator,
        TileCollisionResolver collisionResolver,
        Func<WorldSaveService>? worldSaveServiceFactory = null)
    {
        _projectContentLoader = projectContentLoader ?? throw new ArgumentNullException(nameof(projectContentLoader));
        _startupInventory = startupInventory ?? throw new ArgumentNullException(nameof(startupInventory));
        _loadCoordinator = loadCoordinator ?? throw new ArgumentNullException(nameof(loadCoordinator));
        _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
        _worldSaveServiceFactory = worldSaveServiceFactory ?? (() => new WorldSaveService(WorldChunkStorageMode.RegionFiles));
    }

    public GameSessionBootstrapResult LoadOrCreate(GameSessionBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectRootOrContentRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SaveDirectory);
        ArgumentNullException.ThrowIfNull(request.Settings);

        var projectContent = _projectContentLoader.Load(request.ProjectRootOrContentRoot);
        var content = projectContent.Content.Database;
        var startup = ResolveStartup(content, projectContent.Manifest);
        var profile = ResolveWorldProfile(content, projectContent.Manifest, startup, request.Settings);

        if (request.LoadExistingSave &&
            TryLoadExistingSession(request, projectContent, content, startup, profile, out var loadedSession))
        {
            return new GameSessionBootstrapResult(
                loadedSession,
                projectContent,
                profile,
                startup,
                StarterInventory: null,
                LoadedExistingSave: true,
                SpawnAreaChunksLoadedFromSave: 0,
                SpawnAreaChunksGenerated: 0);
        }

        var starterInventory = _startupInventory.BuildPlayerInventory(content.Items, startup);
        var worldBuild = CreateWorld(request, profile, content);
        var player = CreatePlayerAtSpawn(worldBuild.World);
        var entities = new EntityManager();
        var events = new GameEventBus();
        var worldTime = new WorldTime();
        worldTime.SetDay();
        var farmPlots = new FarmPlotManager();
        var equipmentLoadout = new EquipmentLoadout();
        var simulation = new GameSimulation(
            content,
            worldBuild.World,
            worldBuild.Biomes,
            player,
            starterInventory.Inventory,
            entities,
            worldTime,
            events,
            farmPlots: farmPlots,
            equipmentLoadout: equipmentLoadout,
            livingWorld: worldBuild.LivingWorld);
        worldBuild.World.ClearAllDirtyFlags();
        foreach (var chunk in worldBuild.World.Chunks.Values)
        {
            chunk.MarkLightDirty();
        }

        var session = new LoadedGameSession(
            content,
            worldBuild.World,
            player,
            starterInventory.Inventory,
            entities,
            events,
            worldTime,
            simulation,
            worldBuild.World.IsHorizontallyInfinite ? profile : null,
            request.SaveDirectory,
            new TileEntityManager(),
            farmPlots,
            projectContent.Manifest,
            projectContent.Paths,
            startup,
            starterInventory,
            loadedFromSave: false,
            equipmentLoadout: equipmentLoadout,
            characterAppearance: content.Characters.TryGetById("player", out var playerCharacter)
                ? playerCharacter.DefaultAppearance
                : new Game.Core.Characters.CharacterAppearance(),
            infiniteChunkGenerator: worldBuild.InfiniteChunkGenerator);

        return new GameSessionBootstrapResult(
            session,
            projectContent,
            profile,
            startup,
            starterInventory,
            LoadedExistingSave: false,
            worldBuild.SpawnAreaPreload.LoadedFromSave,
            worldBuild.SpawnAreaPreload.Generated);
    }

    private bool TryLoadExistingSession(
        GameSessionBootstrapRequest request,
        GameProjectContentLoadResult projectContent,
        GameContentDatabase content,
        GameStartupDefinition? startup,
        WorldGenerationProfile profile,
        out LoadedGameSession session)
    {
        session = null!;
        if (!_loadCoordinator.CanLoad(request.SaveDirectory))
        {
            return false;
        }

        var events = new GameEventBus();
        var loaded = _loadCoordinator.Load(request.SaveDirectory, content, events: events);
        var worldTime = loaded.WorldTime;
        var livingWorld = CreateLivingWorldRuntime(loaded.World.Metadata.Seed, profile, content);
        if (loaded.WorldEventState is not null)
        {
            livingWorld.RestoreWorldEvents(loaded.WorldEventState);
        }
        var infiniteGenerator = loaded.World.IsHorizontallyInfinite
            ? new InfiniteWorldChunkGenerator(
                regionalPlanner: new WorldRegionPlanner(
                    loaded.World.Metadata.Seed,
                    livingWorld.Profile,
                    content.Biomes,
                    content.StructurePlans.Definitions.ToArray()))
            : null;
        ConfigureSurfaceResolver(livingWorld, loaded.World, profile, infiniteGenerator);
        var simulation = new GameSimulation(
            content,
            loaded.World,
            CreateFallbackBiomeMap(content),
            loaded.Player,
            loaded.Inventory,
            loaded.Entities,
            worldTime,
            events,
            farmPlots: loaded.FarmPlots,
            equipmentLoadout: loaded.EquipmentLoadout,
            livingWorld: livingWorld,
            randomStreams: loaded.RandomStreams);
        session = new LoadedGameSession(
            content,
            loaded.World,
            loaded.Player,
            loaded.Inventory,
            loaded.Entities,
            events,
            worldTime,
            simulation,
            loaded.World.IsHorizontallyInfinite ? profile : null,
            request.SaveDirectory,
            loaded.TileEntities,
            loaded.FarmPlots,
            projectContent.Manifest,
            projectContent.Paths,
            startup,
            startupInventory: null,
            loadedFromSave: true,
            equipmentLoadout: loaded.EquipmentLoadout,
            characterAppearance: loaded.CharacterAppearance,
            playerLoadWarnings: loaded.PlayerWarnings,
            infiniteChunkGenerator: infiniteGenerator);
        return true;
    }

    private static GameStartupDefinition? ResolveStartup(GameContentDatabase content, GameProjectManifest manifest)
    {
        return content.GameStartups.TryGetDefault(manifest.StartupDefinitionId, out var startup)
            ? startup
            : null;
    }

    private static WorldGenerationProfile ResolveWorldProfile(
        GameContentDatabase content,
        GameProjectManifest manifest,
        GameStartupDefinition? startup,
        GameSettings settings)
    {
        if (content.WorldGenerationProfiles.TryGetById(settings.World.WorldProfileId, out var settingsProfile))
        {
            return settingsProfile;
        }

        if (!string.IsNullOrWhiteSpace(startup?.WorldProfileId) &&
            content.WorldGenerationProfiles.TryGetById(startup.WorldProfileId, out var startupProfile))
        {
            return startupProfile;
        }

        if (!string.IsNullOrWhiteSpace(manifest.DefaultWorldProfileId) &&
            content.WorldGenerationProfiles.TryGetById(manifest.DefaultWorldProfileId, out var manifestProfile))
        {
            return manifestProfile;
        }

        return WorldGenerationProfile.Small;
    }

    private WorldBuildResult CreateWorld(
        GameSessionBootstrapRequest request,
        WorldGenerationProfile profile,
        GameContentDatabase content)
    {
        if (request.Settings.World.InfiniteHorizontalGeneration)
        {
            return CreateInfiniteWorld(profile, request, content);
        }

        var generation = new AdvancedWorldGenerator().GenerateDetailed(profile, request.Seed);
        var world = generation.World;
        world.SetMetadata(world.Metadata with { Name = NormalizeWorldName(request.WorldName) });
        RecalculateSpawnLighting(world);
        var livingWorld = CreateLivingWorldRuntime(request.Seed, profile, content);
        ConfigureSurfaceResolver(livingWorld, world, profile, generator: null);
        return new WorldBuildResult(
            world,
            generation.Biomes,
            SpawnAreaPreloadResult.Empty,
            livingWorld,
            InfiniteChunkGenerator: null);
    }

    private WorldBuildResult CreateInfiniteWorld(
        WorldGenerationProfile profile,
        GameSessionBootstrapRequest request,
        GameContentDatabase content)
    {
        var livingWorld = CreateLivingWorldRuntime(request.Seed, profile, content);
        var generator = new InfiniteWorldChunkGenerator(
            regionalPlanner: new WorldRegionPlanner(
                request.Seed,
                livingWorld.Profile,
                content.Biomes,
                content.StructurePlans.Definitions.ToArray()));
        var world = generator.CreateWorld(profile, request.Seed, NormalizeWorldName(request.WorldName));
        ConfigureSurfaceResolver(livingWorld, world, profile, generator);
        var preload = PreloadSpawnArea(
            world,
            profile,
            generator,
            request.Settings.World.PreloadFullVerticalSlice,
            request.SaveDirectory);
        return new WorldBuildResult(world, CreateFallbackBiomeMap(content), preload, livingWorld, generator);
    }

    private static void ConfigureSurfaceResolver(
        LivingWorldRuntime livingWorld,
        GameWorld world,
        WorldGenerationProfile profile,
        InfiniteWorldChunkGenerator? generator)
    {
        if (generator is not null)
        {
            livingWorld.ConfigureSurfaceHeightResolver(
                generator.CreateSurfaceHeightResolver(profile, world.Metadata.Seed));
            return;
        }

        livingWorld.ConfigureSurfaceHeightResolver(tileX => ResolveLoadedSurfaceHeight(world, tileX, profile.SurfaceBaseY));
    }

    private static int ResolveLoadedSurfaceHeight(GameWorld world, int tileX, int fallback)
    {
        if (!world.IsInBounds(tileX, 0))
        {
            return fallback;
        }

        for (var tileY = 0; tileY < world.HeightTiles; tileY++)
        {
            if (world.TryGetTile(tileX, tileY, out var tile) && tile.IsSolid)
            {
                return tileY;
            }
        }

        return fallback;
    }

    private SpawnAreaPreloadResult PreloadSpawnArea(
        GameWorld world,
        WorldGenerationProfile profile,
        InfiniteWorldChunkGenerator generator,
        bool fullVerticalSlice,
        string saveDirectory)
    {
        var saves = _worldSaveServiceFactory();
        var loaded = 0;
        var generated = 0;
        var spawnChunk = CoordinateUtils.TileToChunk(world.Metadata.SpawnTile);
        var minChunkY = fullVerticalSlice ? 0 : Math.Max(0, spawnChunk.Y - 2);
        var maxChunkY = fullVerticalSlice
            ? CoordinateUtils.TileToChunk(0, profile.HeightTiles - 1).Y
            : Math.Min(CoordinateUtils.TileToChunk(0, profile.HeightTiles - 1).Y, spawnChunk.Y + 2);

        for (var cy = minChunkY; cy <= maxChunkY; cy++)
        {
            for (var cx = spawnChunk.X - 3; cx <= spawnChunk.X + 3; cx++)
            {
                var position = new ChunkPos(cx, cy);
                if (saves.TryLoadChunk(world, saveDirectory, position))
                {
                    loaded++;
                    continue;
                }

                if (generator.EnsureChunk(world, profile, position))
                {
                    generated++;
                }
            }
        }

        return new SpawnAreaPreloadResult(loaded, generated);
    }

    private PlayerEntity CreatePlayerAtSpawn(GameWorld world)
    {
        var spawn = new Vector2(
            world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            world.Metadata.SpawnTile.Y * GameConstants.TileSize);
        return new PlayerEntity(spawn, _collisionResolver);
    }

    private static void RecalculateSpawnLighting(GameWorld world)
    {
        var spawn = new Vector2(
            world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            world.Metadata.SpawnTile.Y * GameConstants.TileSize);

        new LightingSystem().Recalculate(world, new[]
        {
            new LightSource(CoordinateUtils.WorldToTile(spawn.X, spawn.Y), 235, Radius: 9)
        });
    }

    private static string NormalizeWorldName(string worldName)
    {
        return string.IsNullOrWhiteSpace(worldName) ? "New World" : worldName.Trim();
    }

    private static BiomeMap CreateFallbackBiomeMap(GameContentDatabase? content)
    {
        var fallback = content?.Biomes.Definitions.FirstOrDefault()?.Id ?? "forest";
        return new BiomeMap(fallback);
    }

    private static LivingWorldRuntime CreateLivingWorldRuntime(
        int seed,
        WorldGenerationProfile profile,
        GameContentDatabase content)
    {
        var regionalProfile = content.RegionalGenerationProfiles.Profiles
            .OrderBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (regionalProfile is null)
        {
            return LivingWorldRuntime.CreateDefault(
                seed,
                profile.HeightTiles,
                profile.SurfaceBaseY,
                content.Biomes);
        }

        var caveMinDepth = Math.Clamp(
            Math.Max(regionalProfile.CaveMinDepth, profile.SurfaceBaseY + 8),
            0,
            Math.Max(0, profile.HeightTiles - 1));
        var adapted = regionalProfile with
        {
            WorldHeightTiles = profile.HeightTiles,
            SurfaceBaseY = profile.SurfaceBaseY,
            CaveMinDepth = caveMinDepth,
            CaveMaxDepth = Math.Max(caveMinDepth, profile.HeightTiles - 8)
        };
        return new LivingWorldRuntime(
            seed,
            adapted,
            content.Biomes,
            content.StructurePlans.Definitions.ToArray());
    }

    private sealed record WorldBuildResult(
        GameWorld World,
        BiomeMap Biomes,
        SpawnAreaPreloadResult SpawnAreaPreload,
        LivingWorldRuntime LivingWorld,
        InfiniteWorldChunkGenerator? InfiniteChunkGenerator);
}
