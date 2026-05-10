using Game.Core.World;
using MessagePack;

namespace Game.Core.Saving;

public sealed class ChunkRegionStore
{
    public const int DefaultRegionSizeChunks = 8;

    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    private readonly ChunkBinarySerializer _chunkSerializer;

    public ChunkRegionStore()
        : this(new ChunkBinarySerializer())
    {
    }

    public ChunkRegionStore(ChunkBinarySerializer chunkSerializer, int regionSizeChunks = DefaultRegionSizeChunks)
    {
        _chunkSerializer = chunkSerializer ?? throw new ArgumentNullException(nameof(chunkSerializer));
        if (regionSizeChunks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regionSizeChunks), "Region size must be greater than zero.");
        }

        RegionSizeChunks = regionSizeChunks;
    }

    public int RegionSizeChunks { get; }

    public void SaveChunk(string regionDirectory, Chunk chunk)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionDirectory);
        ArgumentNullException.ThrowIfNull(chunk);

        SaveChunks(regionDirectory, new[] { chunk });
    }

    public void SaveChunks(string regionDirectory, IEnumerable<Chunk> chunks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionDirectory);
        ArgumentNullException.ThrowIfNull(chunks);

        Directory.CreateDirectory(regionDirectory);
        foreach (var group in chunks.GroupBy(chunk => ChunkRegionPos.FromChunk(chunk.Position, RegionSizeChunks)))
        {
            var region = LoadRegionOrCreate(regionDirectory, group.Key);
            var entries = region.Chunks.ToDictionary(entry => new ChunkPos(entry.ChunkX, entry.ChunkY));

            foreach (var chunk in group)
            {
                entries[chunk.Position] = new RegionChunkSaveDto(
                    chunk.Position.X,
                    chunk.Position.Y,
                    _chunkSerializer.Serialize(chunk));
            }

            WriteRegion(regionDirectory, new ChunkRegionSaveDto(
                ChunkRegionSaveDto.CurrentVersion,
                group.Key.X,
                group.Key.Y,
                RegionSizeChunks,
                entries.Values
                    .OrderBy(entry => entry.ChunkY)
                    .ThenBy(entry => entry.ChunkX)
                    .ToArray()));
        }
    }

    public bool TryLoadChunk(string regionDirectory, ChunkPos position, out Chunk? chunk)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionDirectory);

        chunk = null;
        var regionPosition = ChunkRegionPos.FromChunk(position, RegionSizeChunks);
        var path = GetRegionFilePath(regionDirectory, regionPosition);
        if (!File.Exists(path))
        {
            return false;
        }

        var region = ReadRegion(path);
        var entry = region.Chunks.FirstOrDefault(chunk => chunk.ChunkX == position.X && chunk.ChunkY == position.Y);
        if (entry is null)
        {
            return false;
        }

        var loaded = _chunkSerializer.Deserialize(entry.Payload);
        if (loaded.Position != position)
        {
            throw new InvalidDataException(
                $"Region file '{path}' entry {position} contains chunk {loaded.Position}.");
        }

        chunk = loaded;
        return true;
    }

    public IReadOnlyList<Chunk> LoadAllChunks(string regionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionDirectory);

        if (!Directory.Exists(regionDirectory))
        {
            return Array.Empty<Chunk>();
        }

        var chunks = new List<Chunk>();
        foreach (var path in Directory.EnumerateFiles(regionDirectory, "*.region", SearchOption.TopDirectoryOnly))
        {
            var region = ReadRegion(path);
            foreach (var entry in region.Chunks)
            {
                var chunk = _chunkSerializer.Deserialize(entry.Payload);
                if (chunk.Position != new ChunkPos(entry.ChunkX, entry.ChunkY))
                {
                    throw new InvalidDataException(
                        $"Region file '{path}' entry ({entry.ChunkX}, {entry.ChunkY}) contains chunk {chunk.Position}.");
                }

                chunks.Add(chunk);
            }
        }

        return chunks
            .OrderBy(chunk => chunk.Position.Y)
            .ThenBy(chunk => chunk.Position.X)
            .ToArray();
    }

    public string GetRegionFilePath(string regionDirectory, ChunkRegionPos region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionDirectory);
        return Path.Combine(regionDirectory, GetRegionFileName(region));
    }

    public static string GetRegionFileName(ChunkRegionPos region)
    {
        return $"r.{region.X}.{region.Y}.region";
    }

    private ChunkRegionSaveDto LoadRegionOrCreate(string regionDirectory, ChunkRegionPos region)
    {
        var path = GetRegionFilePath(regionDirectory, region);
        return File.Exists(path)
            ? ReadRegion(path)
            : new ChunkRegionSaveDto(
                ChunkRegionSaveDto.CurrentVersion,
                region.X,
                region.Y,
                RegionSizeChunks,
                Array.Empty<RegionChunkSaveDto>());
    }

    private void WriteRegion(string regionDirectory, ChunkRegionSaveDto region)
    {
        var path = GetRegionFilePath(regionDirectory, new ChunkRegionPos(region.RegionX, region.RegionY));
        var payload = MessagePackSerializer.Serialize(region, Options);
        File.WriteAllBytes(path, payload);
    }

    private ChunkRegionSaveDto ReadRegion(string path)
    {
        var region = MessagePackSerializer.Deserialize<ChunkRegionSaveDto>(File.ReadAllBytes(path), Options);
        if (region.Version != ChunkRegionSaveDto.CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported chunk region version {region.Version} in '{path}'.");
        }

        if (region.RegionSizeChunks != RegionSizeChunks)
        {
            throw new InvalidDataException(
                $"Region file '{path}' uses region size {region.RegionSizeChunks}, expected {RegionSizeChunks}.");
        }

        return region;
    }

    [MessagePackObject]
    public sealed record ChunkRegionSaveDto
    {
        public const int CurrentVersion = 1;

        [SerializationConstructor]
        public ChunkRegionSaveDto(
            int version,
            int regionX,
            int regionY,
            int regionSizeChunks,
            RegionChunkSaveDto[] chunks)
        {
            Version = version;
            RegionX = regionX;
            RegionY = regionY;
            RegionSizeChunks = regionSizeChunks;
            Chunks = chunks;
        }

        [Key(0)]
        public int Version { get; }

        [Key(1)]
        public int RegionX { get; }

        [Key(2)]
        public int RegionY { get; }

        [Key(3)]
        public int RegionSizeChunks { get; }

        [Key(4)]
        public RegionChunkSaveDto[] Chunks { get; }
    }

    [MessagePackObject]
    public sealed record RegionChunkSaveDto
    {
        [SerializationConstructor]
        public RegionChunkSaveDto(int chunkX, int chunkY, byte[] payload)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            Payload = payload;
        }

        [Key(0)]
        public int ChunkX { get; }

        [Key(1)]
        public int ChunkY { get; }

        [Key(2)]
        public byte[] Payload { get; }
    }
}
