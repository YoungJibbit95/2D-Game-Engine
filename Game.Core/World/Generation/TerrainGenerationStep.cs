namespace Game.Core.World.Generation;

public sealed class TerrainGenerationStep : IWorldGenerationStep
{
    public string Name => "terrain";

    public int Order => 0;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var tiles = context.Tiles;
        var profile = context.Profile;
        var topologyOriginX = world.WidthTiles / 2;

        for (var x = 0; x < world.WidthTiles; x++)
        {
            var topologyX = x - topologyOriginX;
            var surfaceY = WorldSurfaceSampler.GetSurfaceHeight(profile, context.Seed, topologyX);
            context.SurfaceHeights[x] = surfaceY;

            var dirtDepth = WorldSurfaceSampler.GetDirtDepth(profile, context.Seed, topologyX);
            for (var y = surfaceY; y < world.HeightTiles; y++)
            {
                var tileId = y == surfaceY
                    ? KnownTileIds.Grass
                    : y < surfaceY + dirtDepth
                        ? KnownTileIds.Dirt
                        : KnownTileIds.Stone;

                tiles.SetTile(x, y, TileInstance.FromTileId(tileId, TileFlags.IsNatural));
            }
        }
    }

}
