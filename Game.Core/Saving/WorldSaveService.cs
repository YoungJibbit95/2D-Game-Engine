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

    public WorldSaveService()
        : this(new ChunkBinarySerializer())
    {
    }

    public WorldSaveService(ChunkBinarySerializer chunkSerializer)
    {
        _chunkSerializer = chunkSerializer;
    }

    public void Save(World.World world, string worldDirectory, WorldSaveMode mode = WorldSaveMode.AllChunks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);

        Directory.CreateDirectory(worldDirectory);
        var chunkDirectory = Path.Combine(worldDirectory, ChunkDirectoryName);
        Directory.CreateDirectory(chunkDirectory);

        SaveMetadata(world, worldDirectory);
        SaveChunks(world, chunkDirectory, mode);
        world.ClearAllDirtyFlags();
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
                new TilePos(metadata.SpawnTileX, metadata.SpawnTileY)));

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

        world.ClearAllDirtyFlags();
        return world;
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
        }
    }

    private static string GetChunkFileName(ChunkPos position)
    {
        return $"{position.X}_{position.Y}.bin";
    }
}
