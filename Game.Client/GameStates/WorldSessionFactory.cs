using Game.Client.Configuration;
using Game.Core;
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
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.TileEntities;
using System.Numerics;

namespace Game.Client.GameStates;

public static class WorldSessionFactory
{
    public static LoadedGameSession CreateSingleplayer(int seed, string worldName)
    {
        var projectContent = new GameProjectContentLoader().Load(ClientPaths.FindGameProjectPaths().ProjectRoot);
        var content = projectContent.Content.Database;
        var settings = LoadSettings();
        var profile = ResolveWorldProfile(content, projectContent.Manifest, settings);
        var saveDirectory = ClientPaths.WorldSaveDirectory(worldName, seed);

        if (TryLoadExistingSession(content, profile, saveDirectory, out var loadedSession))
        {
            return loadedSession;
        }

        var inventory = new PlayerInventory(content.Items);
        GiveStarterItems(inventory);

        var world = settings.World.InfiniteHorizontalGeneration
            ? CreateInfiniteWorld(profile, seed, worldName, settings, saveDirectory)
            : CreateFiniteWorld(profile, seed, worldName);

        var collisionResolver = new TileCollisionResolver();
        var spawn = new Vector2(
            world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            world.Metadata.SpawnTile.Y * GameConstants.TileSize);
        var player = new PlayerEntity(spawn, collisionResolver);

        world.ClearAllDirtyFlags();
        return new LoadedGameSession(
            content,
            world,
            player,
            inventory,
            new EntityManager(),
            new GameEventBus(),
            new WorldTime(),
            world.IsHorizontallyInfinite ? profile : null,
            saveDirectory,
            new TileEntityManager(),
            new FarmPlotManager());
    }

    private static bool TryLoadExistingSession(
        GameContentDatabase content,
        WorldGenerationProfile profile,
        string saveDirectory,
        out LoadedGameSession session)
    {
        session = null!;
        var events = new GameEventBus();
        var loader = new GameLoadCoordinator();
        if (!loader.CanLoad(saveDirectory))
        {
            return false;
        }

        var loaded = loader.Load(saveDirectory, content, events: events);
        session = new LoadedGameSession(
            content,
            loaded.World,
            loaded.Player,
            loaded.Inventory,
            loaded.Entities,
            events,
            new WorldTime(),
            loaded.World.IsHorizontallyInfinite ? profile : null,
            saveDirectory,
            loaded.TileEntities,
            loaded.FarmPlots);
        return true;
    }

    private static WorldGenerationProfile ResolveWorldProfile(
        GameContentDatabase content,
        GameProjectManifest manifest,
        GameSettings settings)
    {
        if (content.WorldGenerationProfiles.TryGetById(settings.World.WorldProfileId, out var settingsProfile))
        {
            return settingsProfile;
        }

        if (!string.IsNullOrWhiteSpace(manifest.DefaultWorldProfileId) &&
            content.WorldGenerationProfiles.TryGetById(manifest.DefaultWorldProfileId, out var manifestProfile))
        {
            return manifestProfile;
        }

        return WorldGenerationProfile.Small;
    }

    private static void GiveStarterItems(PlayerInventory inventory)
    {
        inventory.AddItem(new ItemStack("copper_pickaxe", 1));
        inventory.AddItem(new ItemStack("copper_hoe", 1));
        inventory.AddItem(new ItemStack("watering_can", 1));
        inventory.AddItem(new ItemStack("parsnip_seeds", 12));
        inventory.AddItem(new ItemStack("dirt_block", 50));
        inventory.AddItem(new ItemStack("stone_block", 25));
    }

    private static World CreateInfiniteWorld(
        WorldGenerationProfile profile,
        int seed,
        string worldName,
        GameSettings settings,
        string saveDirectory)
    {
        var infiniteWorldgen = new InfiniteWorldChunkGenerator();
        var world = infiniteWorldgen.CreateWorld(profile, seed, worldName);
        PreloadSpawnArea(world, profile, infiniteWorldgen, settings.World.PreloadFullVerticalSlice, saveDirectory);
        return world;
    }

    private static World CreateFiniteWorld(WorldGenerationProfile profile, int seed, string worldName)
    {
        var result = new AdvancedWorldGenerator().GenerateDetailed(profile, seed);
        var world = result.World;
        world.SetMetadata(world.Metadata with { Name = worldName });
        var spawn = new Vector2(
            world.Metadata.SpawnTile.X * GameConstants.TileSize + 2,
            world.Metadata.SpawnTile.Y * GameConstants.TileSize);

        new LightingSystem().Recalculate(world, new[]
        {
            new LightSource(CoordinateUtils.WorldToTile(spawn.X, spawn.Y), 235, Radius: 9)
        });

        return world;
    }

    private static void PreloadSpawnArea(
        World world,
        WorldGenerationProfile profile,
        InfiniteWorldChunkGenerator generator,
        bool fullVerticalSlice,
        string saveDirectory)
    {
        var saves = new WorldSaveService(WorldChunkStorageMode.RegionFiles);
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
                if (!saves.TryLoadChunk(world, saveDirectory, position))
                {
                    generator.EnsureChunk(world, profile, position);
                }
            }
        }
    }

    private static GameSettings LoadSettings()
    {
        try
        {
            return new GameSettingsService().LoadOrCreate(ClientPaths.SettingsPath());
        }
        catch (InvalidDataException)
        {
            return GameSettings.CreateDefault();
        }
    }
}
