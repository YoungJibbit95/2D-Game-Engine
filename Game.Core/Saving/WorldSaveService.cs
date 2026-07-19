using Game.Core.World;
using System.Text.Json;

namespace Game.Core.Saving;

public sealed class WorldSaveService
{
    private const string MetadataFileName = "metadata.json";
    private const string ChunkDirectoryName = "chunks";
    private const string RegionDirectoryName = "regions";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ChunkBinarySerializer _chunkSerializer;
    private readonly ChunkRegionStore _regionStore;
    private readonly ChunkMetadataService _chunkMetadata;
    private readonly WorldChunkStorageMode _chunkStorageMode;
    private long _saveTick;

    public WorldSaveService()
        : this(new ChunkBinarySerializer(), new ChunkMetadataService())
    {
    }

    public WorldSaveService(WorldChunkStorageMode chunkStorageMode)
        : this(new ChunkBinarySerializer(), new ChunkMetadataService(), chunkStorageMode)
    {
    }

    public WorldSaveService(
        ChunkBinarySerializer chunkSerializer,
        ChunkMetadataService? chunkMetadata = null,
        WorldChunkStorageMode chunkStorageMode = WorldChunkStorageMode.LooseFiles,
        ChunkRegionStore? regionStore = null)
    {
        _chunkSerializer = chunkSerializer ?? throw new ArgumentNullException(nameof(chunkSerializer));
        _regionStore = regionStore ?? new ChunkRegionStore(chunkSerializer);
        _chunkMetadata = chunkMetadata ?? new ChunkMetadataService();
        _chunkStorageMode = chunkStorageMode;
    }

    public void Save(World.World world, string worldDirectory, WorldSaveMode mode = WorldSaveMode.AllChunks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        Directory.CreateDirectory(worldDirectory);
        var chunkDirectory = GetChunkStorageDirectory(worldDirectory);
        Directory.CreateDirectory(chunkDirectory);

        SaveMetadata(world, worldDirectory);
        _saveTick++;
        SaveChunks(world, chunkDirectory, mode);
        world.ClearAllDirtyFlags();
    }

    public bool SaveChunk(World.World world, string worldDirectory, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        if (!world.TryGetChunk(position, out var chunk) || chunk is null)
        {
            return false;
        }

        Directory.CreateDirectory(worldDirectory);
        var chunkDirectory = GetChunkStorageDirectory(worldDirectory);
        Directory.CreateDirectory(chunkDirectory);

        SaveMetadata(world, worldDirectory);
        _saveTick++;
        if (_chunkStorageMode == WorldChunkStorageMode.RegionFiles)
        {
            _regionStore.SaveChunk(chunkDirectory, chunk);
        }
        else
        {
            var path = Path.Combine(chunkDirectory, GetChunkFileName(position));
            File.WriteAllBytes(path, _chunkSerializer.Serialize(chunk));
        }

        _chunkMetadata.MarkSaved(chunk, _saveTick);
        chunk.ClearDirtyFlags();
        return true;
    }

    public World.World Load(string worldDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        var metadataPath = Path.Combine(worldDirectory, MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException("World metadata file was not found.", metadataPath);
        }

        var metadata = JsonSerializer.Deserialize<WorldSaveMetadata>(File.ReadAllText(metadataPath), JsonOptions)
            ?? throw new InvalidDataException("World metadata file was empty.");

        var world = new World.World(
            metadata.WidthTiles,
            metadata.HeightTiles,
            new WorldMetadata(
                metadata.Name,
                metadata.Seed,
                metadata.CreatedAtUtc,
                new TilePos(metadata.SpawnTileX, metadata.SpawnTileY))
            {
                GenerationVersion = Game.Core.World.Generation.WorldGenerationVersions.Normalize(
                    metadata.GenerationVersion),
                GenerationProfileId = metadata.GenerationProfileId ?? string.Empty
            },
            metadata.IsHorizontallyInfinite);

        var storageMode = ResolveStorageMode(metadata);
        var loadedAny = storageMode == WorldChunkStorageMode.RegionFiles
            ? LoadRegionChunks(world, Path.Combine(worldDirectory, RegionDirectoryName))
            : LoadLooseChunks(world, Path.Combine(worldDirectory, ChunkDirectoryName));

        if (!loadedAny)
        {
            if (storageMode == WorldChunkStorageMode.RegionFiles)
            {
                LoadLooseChunks(world, Path.Combine(worldDirectory, ChunkDirectoryName));
            }
            else
            {
                LoadRegionChunks(world, Path.Combine(worldDirectory, RegionDirectoryName));
            }
        }

        _chunkMetadata.RefreshAll(world);
        world.ClearAllDirtyFlags();
        return world;
    }

    public bool TryLoadChunk(World.World world, string worldDirectory, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        var regionDirectory = Path.Combine(worldDirectory, RegionDirectoryName);
        if (_regionStore.TryLoadChunk(regionDirectory, position, out var regionChunk) && regionChunk is not null)
        {
            world.GetOrCreateChunk(position).LoadTiles(regionChunk.Tiles);
            _chunkMetadata.RefreshChunk(world, position);
            return true;
        }

        var path = Path.Combine(worldDirectory, ChunkDirectoryName, GetChunkFileName(position));
        if (!File.Exists(path))
        {
            return false;
        }

        var loaded = _chunkSerializer.Deserialize(File.ReadAllBytes(path));
        if (loaded.Position != position)
        {
            throw new InvalidDataException(
                $"Chunk file '{path}' contains {loaded.Position}, but {position} was requested.");
        }

        world.GetOrCreateChunk(position).LoadTiles(loaded.Tiles);
        _chunkMetadata.RefreshChunk(world, position);
        return true;
    }

    private void SaveMetadata(World.World world, string worldDirectory)
    {
        var metadata = new WorldSaveMetadata
        {
            Name = world.Metadata.Name,
            Seed = world.Metadata.Seed,
            GenerationVersion = world.Metadata.GenerationVersion,
            GenerationProfileId = world.Metadata.GenerationProfileId,
            CreatedAtUtc = world.Metadata.CreatedAtUtc,
            WidthTiles = world.WidthTiles,
            HeightTiles = world.HeightTiles,
            IsHorizontallyInfinite = world.IsHorizontallyInfinite,
            ChunkStorageMode = _chunkStorageMode.ToString(),
            SpawnTileX = world.Metadata.SpawnTile.X,
            SpawnTileY = world.Metadata.SpawnTile.Y
        };

        File.WriteAllText(Path.Combine(worldDirectory, MetadataFileName), JsonSerializer.Serialize(metadata, JsonOptions));
    }

    private void SaveChunks(World.World world, string chunkDirectory, WorldSaveMode mode)
    {
        var chunksToSave = new List<Chunk>();
        foreach (var chunk in world.Chunks.Values.OrderBy(chunk => chunk.Position.Y).ThenBy(chunk => chunk.Position.X))
        {
            if (mode == WorldSaveMode.DirtyChunksOnly && !chunk.IsDirty)
            {
                continue;
            }

            chunksToSave.Add(chunk);
        }

        if (_chunkStorageMode == WorldChunkStorageMode.RegionFiles)
        {
            _regionStore.SaveChunks(chunkDirectory, chunksToSave);
            foreach (var chunk in chunksToSave)
            {
                _chunkMetadata.MarkSaved(chunk, _saveTick);
            }

            return;
        }

        foreach (var chunk in chunksToSave)
        {
            var path = Path.Combine(chunkDirectory, GetChunkFileName(chunk.Position));
            File.WriteAllBytes(path, _chunkSerializer.Serialize(chunk));
            _chunkMetadata.MarkSaved(chunk, _saveTick);
        }
    }

    private bool LoadLooseChunks(World.World world, string chunkDirectory)
    {
        if (!Directory.Exists(chunkDirectory))
        {
            return false;
        }

        var loaded = false;
        foreach (var chunkFile in Directory.EnumerateFiles(chunkDirectory, "*.bin", SearchOption.TopDirectoryOnly))
        {
            var chunk = _chunkSerializer.Deserialize(File.ReadAllBytes(chunkFile));
            world.GetOrCreateChunk(chunk.Position).LoadTiles(chunk.Tiles);
            loaded = true;
        }

        return loaded;
    }

    private bool LoadRegionChunks(World.World world, string regionDirectory)
    {
        var chunks = _regionStore.LoadAllChunks(regionDirectory);
        foreach (var chunk in chunks)
        {
            world.GetOrCreateChunk(chunk.Position).LoadTiles(chunk.Tiles);
        }

        return chunks.Count > 0;
    }

    private string GetChunkStorageDirectory(string worldDirectory)
    {
        return Path.Combine(
            worldDirectory,
            _chunkStorageMode == WorldChunkStorageMode.RegionFiles ? RegionDirectoryName : ChunkDirectoryName);
    }

    private static WorldChunkStorageMode ResolveStorageMode(WorldSaveMetadata metadata)
    {
        return Enum.TryParse<WorldChunkStorageMode>(metadata.ChunkStorageMode, ignoreCase: true, out var mode)
            ? mode
            : WorldChunkStorageMode.LooseFiles;
    }

    private static string GetChunkFileName(ChunkPos position)
    {
        return $"{position.X}_{position.Y}.bin";
    }
}
