using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public readonly record struct TreeTileMaterial(ushort TrunkTileId, ushort CanopyTileId)
{
    public static TreeTileMaterial Legacy { get; } = new(KnownTileIds.Wood, KnownTileIds.Leaves);
}

public static class TreeMaterialResolver
{
    public static TreeTileMaterial Resolve(BiomeDefinition? biome)
    {
        if (biome is null || string.IsNullOrWhiteSpace(biome.TreeType))
        {
            return TreeTileMaterial.Legacy;
        }

        return KnownTileIds.TryResolveContentId(biome.TreeMaterial.TrunkTile, out var trunkTileId) &&
               KnownTileIds.TryResolveContentId(biome.TreeMaterial.CanopyTile, out var canopyTileId)
            ? new TreeTileMaterial(trunkTileId, canopyTileId)
            : TreeTileMaterial.Legacy;
    }
}
