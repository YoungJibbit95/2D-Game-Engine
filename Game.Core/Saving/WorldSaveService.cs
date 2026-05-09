using Game.Core.World;
using System.Text.Json;

namespace Game.Core.Saving;

public sealed class WorldSaveService
{
    private const string MetadataFileName = "metadata.json";
    private const string ChunkDirectoryName = "chunks";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ChunkBinarySerializer _chunkSerializer;
    private readonly ChunkMetadataService _chunkMetadata;
    private long _saveTick;

    public WorldSaveService()
        : this(new ChunkBinarySerializer(), new ChunkMetadataService())
    {
    }

    public WorldSaveService(ChunkBinarySerializer chunkSerializer, ChunkMetadataService? chunkMetadata = null)
    {
        _chunkSerializer = chunkSerializer;
        _chunkMetadata = chunkMetadata ?? new ChunkMetadataService();
    }

    public void Save(World.World world, string worldDirectory, WorldSaveMode mode = WorldSaveMode.AllChunks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        Directory.CreateDirectory(worldDirectory);
        var chunkDirectory = Path.Combine(worldDirectory, ChunkDirectoryName);
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
        var chunkDirectory = Path.Combine(worldDirectory, ChunkDirectoryName);
        Directory.CreateDirectory(chunkDirectory);

        SaveMetadata(world, worldDirectory);
        _saveTick++;
        var path = Path.Combine(chunkDirectory, GetChunkFileName(position));
        File.WriteAllBytes(path, _chunkSerializer.Serialize(chunk));
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
                new TilePos(metadata.SpawnTileX, metadata.SpawnTileY)),
            metadata.IsHorizontallyInfinite);

        var chunkDirectory = Path.Combine(worldDirectory, ChunkDirectoryName);
        if (!Directory.Exists(chunkDirectory))
        {
            return world;
        }

        foreach (var chunkFile in Directory.EnumerateFiles(chunkDirectory, "*.bin", SearchOption.TopDirectoryOnly))
        {
            var chunk = _chunkSerializer.Deserialize(File.ReadAllBytes(chunkFile));
            world.GetOrCreateChunk(chunk.Position).LoadTiles(chunk.Tiles);
        }

        _chunkMetadata.RefreshAll(world);
        world.ClearAllDirtyFlags();
        return world;
    }

    public bool TryLoadChunk(World.World world, string worldDirectory, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

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

    private static void SaveMetadata(World.World world, string worldDirectory)
    {
        var metadata = new WorldSaveMetadata
        {
            Name = world.Metadata.Name,
            Seed = world.Metadata.Seed,
            CreatedAtUtc = world.Metadata.CreatedAtUtc,
            WidthTiles = world.WidthTiles,
            HeightTiles = world.HeightTiles,
            IsHorizontallyInfinite = world.IsHorizontallyInfinite,
            SpawnTileX = world.Metadata.SpawnTile.X,
            SpawnTileY = world.Metadata.SpawnTile.Y
        };

        File.WriteAllText(Path.Combine(worldDirectory, MetadataFileName), JsonSerializer.Serialize(metadata, JsonOptions));
    }

    private void SaveChunks(World.World world, string chunkDirectory, WorldSaveMode mode)
    {
        foreach (var chunk in world.Chunks.Values.OrderBy(chunk => chunk.Position.Y).ThenBy(chunk => chunk.Position.X))
        {
            if (mode == WorldSaveMode.DirtyChunksOnly && !chunk.IsDirty)
            {
                continue;
            }

            var path = Path.Combine(chunkDirectory, GetChunkFileName(chunk.Position));
            File.WriteAllBytes(path, _chunkSerializer.Serialize(chunk));
            _chunkMetadata.MarkSaved(chunk, _saveTick);
        }
    }

    private static string GetChunkFileName(ChunkPos position)
    {
        return $"{position.X}_{position.Y}.bin";
    }
}
