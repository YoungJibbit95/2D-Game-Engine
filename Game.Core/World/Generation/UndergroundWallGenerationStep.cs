namespace Game.Core.World.Generation;

public sealed class UndergroundWallGenerationStep : IWorldGenerationStep
{
    public string Name => "underground_walls";

    public int Order => 24;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var profile = context.Profile;
        if (profile.DirtWallId == 0 || profile.StoneWallId == 0)
        {
            return;
        }

        var solidCoverage = Math.Clamp(profile.UndergroundWallCoverageChance, 0f, 1f);
        var caveCoverage = Math.Clamp(profile.CaveWallCoverageChance, 0f, 1f);
        var patchScale = Math.Max(0.01f, profile.WallPatchScale);
        var startOffset = Math.Max(0, profile.UndergroundWallStartDepthOffset);
        var dirtDepth = Math.Max(profile.DirtDepthMin, profile.DirtDepthMax);

        for (var x = 0; x < world.WidthTiles; x++)
        {
            var startY = Math.Clamp(context.SurfaceHeights[x] + startOffset, 0, world.HeightTiles);
            for (var y = startY; y < world.HeightTiles; y++)
            {
                var tile = world.GetTile(x, y);
                var coverage = tile.IsSolid ? solidCoverage : caveCoverage;
                if (coverage <= 0f || SamplePatch(context, x, y, patchScale) > coverage)
                {
                    continue;
                }

                var useStoneWall = y >= context.SurfaceHeights[x] + dirtDepth ||
                    tile.TileId is KnownTileIds.Stone or KnownTileIds.CopperOre or KnownTileIds.IronOre;
                WorldGenerationTileMutations.SetWall(
                    world,
                    x,
                    y,
                    useStoneWall ? profile.StoneWallId : profile.DirtWallId);
            }
        }
    }

    private static float SamplePatch(WorldGenerationContext context, int x, int y, float scale)
    {
        var noise = context.Noise.GetNoise((x + 7_919) * scale, (y - 3_571) * scale);
        return Math.Clamp((noise + 1f) * 0.5f, 0f, 1f);
    }
}
