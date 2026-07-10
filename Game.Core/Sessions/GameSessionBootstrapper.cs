using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Lighting;
using Game.Core.Physics;
using Game.Core.Projects;
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
        var worldBuild = CreateWorld(request, profile);
        var player = CreatePlayerAtSpawn(worldBuild.World);
        worldBuild.World.ClearAllDirtyFlags();

        var session = new LoadedGameSession(
            content,
            worldBuild.World,
            player,
            starterInventory.Inventory,
            new EntityManager(),
            new GameEventBus(),
            new WorldTime(),
            worldBuild.World.IsHorizontallyInfinite ? profile : null,
            request.SaveDirectory,
            new TileEntityManager(),
            new FarmPlotManager(),
            projectContent.Manifest,
            projectContent.Paths,
            startup,
            starterInventory,
            LoadedFromSave: false,
            EquipmentLoadout: new Game.Core.Equipment.EquipmentLoadout(),
            CharacterAppearance: content.Characters.TryGetById("player", out var playerCharacter)
                ? playerCharacter.DefaultAppearance
                : new Game.Core.Characters.CharacterAppearance());

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
        session = new LoadedGameSession(
            content,
            loaded.World,
            loaded.Player,
            loaded.Inventory,
            loaded.Entities,
            events,
            new WorldTime(),
            loaded.World.IsHorizontallyInfinite ? profile : null,
            request.SaveDirectory,
            loaded.TileEntities,
            loaded.FarmPlots,
            projectContent.Manifest,
            projectContent.Paths,
            startup,
            StartupInventory: null,
            LoadedFromSave: true,
            EquipmentLoadout: loaded.EquipmentLoadout,
            CharacterAppearance: loaded.CharacterAppearance,
            PlayerLoadWarnings: loaded.PlayerWarnings);
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

    private WorldBuildResult CreateWorld(GameSessionBootstrapRequest request, WorldGenerationProfile profile)
    {
        if (request.Settings.World.InfiniteHorizontalGeneration)
        {
            return CreateInfiniteWorld(profile, request);
        }

        var result = new AdvancedWorldGenerator().GenerateDetailed(profile, request.Seed);
        var world = result.World;
        world.SetMetadata(world.Metadata with { Name = NormalizeWorldName(request.WorldName) });
        RecalculateSpawnLighting(world);
        return new WorldBuildResult(world, SpawnAreaPreloadResult.Empty);
    }

    private WorldBuildResult CreateInfiniteWorld(WorldGenerationProfile profile, GameSessionBootstrapRequest request)
    {
        var generator = new InfiniteWorldChunkGenerator();
        var world = generator.CreateWorld(profile, request.Seed, NormalizeWorldName(request.WorldName));
        var preload = PreloadSpawnArea(
            world,
            profile,
            generator,
            request.Settings.World.PreloadFullVerticalSlice,
            request.SaveDirectory);
        return new WorldBuildResult(world, preload);
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

    private sealed record WorldBuildResult(GameWorld World, SpawnAreaPreloadResult SpawnAreaPreload);
}
