using Game.Core.World;
using K4os.Compression.LZ4;
using MessagePack;

namespace Game.Core.Saving;

public sealed class ChunkBinarySerializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    public byte[] Serialize(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var dto = ChunkSaveDto.FromChunk(chunk);
        var payload = MessagePackSerializer.Serialize(dto, Options);
        return LZ4Pickler.Pickle(payload);
    }

    public Chunk Deserialize(byte[] compressedPayload)
    {
        ArgumentNullException.ThrowIfNull(compressedPayload);

        var payload = LZ4Pickler.Unpickle(compressedPayload);
        var dto = MessagePackSerializer.Deserialize<ChunkSaveDto>(payload, Options);
        var chunk = new Chunk(new ChunkPos(dto.X, dto.Y));
        chunk.LoadTiles(dto.Tiles.Select(tile => tile.ToTileInstance()).ToArray());
        return chunk;
    }

    [MessagePackObject]
    public sealed record ChunkSaveDto
    {
        [SerializationConstructor]
        public ChunkSaveDto(int x, int y, TileSaveDto[] tiles)
        {
            X = x;
            Y = y;
            Tiles = tiles;
        }

        [Key(0)]
        public int X { get; }

        [Key(1)]
        public int Y { get; }

        [Key(2)]
        public TileSaveDto[] Tiles { get; }

        public static ChunkSaveDto FromChunk(Chunk chunk)
        {
            return new ChunkSaveDto(
                chunk.Position.X,
                chunk.Position.Y,
                chunk.Tiles.Select(TileSaveDto.FromTileInstance).ToArray());
        }
    }

    [MessagePackObject]
    public readonly record struct TileSaveDto
    {
        [SerializationConstructor]
        public TileSaveDto(ushort tileId, ushort wallId, byte liquidAmount, byte light, ushort flags)
        {
            TileId = tileId;
            WallId = wallId;
            LiquidAmount = liquidAmount;
            Light = light;
            Flags = flags;
        }

        [Key(0)]
        public ushort TileId { get; }

        [Key(1)]
        public ushort WallId { get; }

        [Key(2)]
        public byte LiquidAmount { get; }

        [Key(3)]
        public byte Light { get; }

        [Key(4)]
        public ushort Flags { get; }

        public static TileSaveDto FromTileInstance(TileInstance tile)
        {
            return new TileSaveDto(tile.TileId, tile.WallId, tile.LiquidAmount, tile.Light, (ushort)tile.Flags);
        }

        public TileInstance ToTileInstance()
        {
            return new TileInstance
            {
                TileId = TileId,
                WallId = WallId,
                LiquidAmount = LiquidAmount,
                Light = Light,
                Flags = (TileFlags)Flags
            };
        }
    }
}
