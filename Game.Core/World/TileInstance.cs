namespace Game.Core.World;

public struct TileInstance
{
    public static readonly TileInstance Air = new()
    {
        TileId = KnownTileIds.Air,
        WallId = 0,
        LiquidAmount = 0,
        Light = 0,
        Flags = TileFlags.None
    };

    public ushort TileId { get; set; }

    public ushort WallId { get; set; }

    public byte LiquidAmount { get; set; }

    public byte Light { get; set; }

    public TileFlags Flags { get; set; }

    public bool IsAir => TileId == KnownTileIds.Air;

    public bool IsSolid => Flags.HasFlag(TileFlags.Solid);

    public bool HasLiquid => Flags.HasFlag(TileFlags.HasLiquid) && LiquidAmount > 0;

    public static TileInstance FromTileId(ushort tileId, TileFlags flags = TileFlags.None, bool isSolid = true)
    {
        if (tileId == KnownTileIds.Air)
        {
            return Air;
        }

        return new TileInstance
        {
            TileId = tileId,
            Flags = isSolid ? flags | TileFlags.Solid : flags & ~TileFlags.Solid
        };
    }

    public static TileInstance Liquid(byte amount)
    {
        return amount == 0
            ? Air
            : new TileInstance
            {
                TileId = KnownTileIds.Air,
                LiquidAmount = amount,
                Flags = TileFlags.HasLiquid
            };
    }
}
