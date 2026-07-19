namespace Game.Core.World;

public static class KnownTileIds
{
    public const ushort Air = 0;
    public const ushort Dirt = 1;
    public const ushort Grass = 2;
    public const ushort Stone = 3;
    public const ushort CopperOre = 4;
    public const ushort IronOre = 5;
    public const ushort Wood = 6;
    public const ushort Leaves = 7;
    public const ushort Workbench = 8;
    public const ushort Furnace = 9;
    public const ushort Anvil = 10;
    public const ushort Torch = 11;
    public const ushort Amberstone = 12;
    public const ushort AmberwoodPlank = 13;
    public const ushort MangroveRoot = 14;
    public const ushort MarshMoss = 15;
    public const ushort OakTrunk = 16;
    public const ushort OakLeaves = 17;
    public const ushort LivingWood = 18;
    public const ushort AutumnLeaves = 19;
    public const ushort MarshLeaves = 20;

    public static bool IsFoliage(ushort tileId)
    {
        return tileId is Leaves or OakLeaves or AutumnLeaves or MarshLeaves;
    }

    public static bool IsTreeTrunk(ushort tileId)
    {
        return tileId is Wood or OakTrunk or LivingWood or MangroveRoot;
    }

    public static bool TryResolveContentId(string? tileId, out ushort resolved)
    {
        resolved = tileId?.Trim().ToLowerInvariant() switch
        {
            "air" => Air,
            "dirt" => Dirt,
            "grass" => Grass,
            "stone" => Stone,
            "copper_ore" => CopperOre,
            "iron_ore" => IronOre,
            "wood" => Wood,
            "leaves" => Leaves,
            "workbench" => Workbench,
            "furnace" => Furnace,
            "anvil" => Anvil,
            "torch" => Torch,
            "amberstone" => Amberstone,
            "amberwood_plank" => AmberwoodPlank,
            "mangrove_root" => MangroveRoot,
            "marsh_moss" => MarshMoss,
            "oak_trunk" => OakTrunk,
            "oak_leaves" => OakLeaves,
            "living_wood" => LivingWood,
            "autumn_leaves" => AutumnLeaves,
            "marsh_leaves" => MarshLeaves,
            _ => ushort.MaxValue
        };

        return resolved != ushort.MaxValue;
    }
}
