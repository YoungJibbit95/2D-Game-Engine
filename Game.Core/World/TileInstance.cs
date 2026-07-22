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

    public bool IsSolid => (Flags & TileFlags.Solid) != 0;

    public bool HasLiquid => (Flags & TileFlags.HasLiquid) != 0 && LiquidAmount > 0;

    public TileCollisionShape CollisionShape
    {
        get
        {
            if (!IsSolid || (Flags & TileFlags.IsActuated) != 0)
            {
                return TileCollisionShape.Empty;
            }

            if ((Flags & TileFlags.Platform) != 0)
            {
                return TileCollisionShape.OneWayPlatform;
            }

            if ((Flags & TileFlags.HalfBlock) != 0)
            {
                return TileCollisionShape.HalfBlock;
            }

            var ascendsLeft = (Flags & TileFlags.SlopeAscendingLeft) != 0;
            var ascendsRight = (Flags & TileFlags.SlopeAscendingRight) != 0;
            if (ascendsLeft && ascendsRight)
            {
                return TileCollisionShape.FullBlock;
            }

            if (ascendsLeft)
            {
                return TileCollisionShape.SlopeAscendingLeft;
            }

            if (ascendsRight || (Flags & TileFlags.Slope) != 0)
            {
                return TileCollisionShape.SlopeAscendingRight;
            }

            return TileCollisionShape.FullBlock;
        }
    }

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
