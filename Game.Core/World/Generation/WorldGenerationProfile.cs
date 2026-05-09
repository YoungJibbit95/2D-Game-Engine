namespace Game.Core.World.Generation;

public sealed record WorldGenerationProfile
{
    public required string Id { get; init; }

    public int WidthTiles { get; init; }

    public int HeightTiles { get; init; }

    public int SurfaceBaseY { get; init; }

    public int SurfaceAmplitude { get; init; }

    public int DirtDepthMin { get; init; }

    public int DirtDepthMax { get; init; }

    public int CaveWalkerCount { get; init; }

    public int CaveWalkLength { get; init; }

    public int CopperVeinCount { get; init; }

    public int IronVeinCount { get; init; }

    public int TreeAttempts { get; init; }

    public int WaterPocketAttempts { get; init; }

    public int CaveMinDepthOffset { get; init; } = 8;

    public int CaveClampDepthOffset { get; init; } = 5;

    public int CaveMinRadius { get; init; } = 1;

    public int CaveMaxRadius { get; init; } = 2;

    public float CaveRadiusChangeChance { get; init; } = 0.08f;

    public int WaterMinDepthOffset { get; init; } = 12;

    public int WaterMinRadiusX { get; init; } = 3;

    public int WaterMaxRadiusX { get; init; } = 7;

    public int WaterMinRadiusY { get; init; } = 2;

    public int WaterMaxRadiusY { get; init; } = 4;

    public float TreeAttemptChance { get; init; } = 0.55f;

    public int TreeMinHeight { get; init; } = 4;

    public int TreeMaxHeight { get; init; } = 7;

    public IReadOnlyList<OreGenerationDefinition> Ores { get; init; } = Array.Empty<OreGenerationDefinition>();

    public IReadOnlyList<WorldDimensionDefinition> Dimensions { get; init; } = Array.Empty<WorldDimensionDefinition>();

    public static WorldGenerationProfile Small { get; } = new()
    {
        Id = "small",
        WidthTiles = 384,
        HeightTiles = 160,
        SurfaceBaseY = 58,
        SurfaceAmplitude = 10,
        DirtDepthMin = 8,
        DirtDepthMax = 18,
        CaveWalkerCount = 18,
        CaveWalkLength = 130,
        CopperVeinCount = 30,
        IronVeinCount = 20,
        TreeAttempts = 44,
        WaterPocketAttempts = 18,
        Ores = new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = 30,
                MinDepthOffset = 8,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = 20,
                MinDepthOffset = 16,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            }
        }
    };

    public static WorldGenerationProfile Medium { get; } = Small with
    {
        Id = "medium",
        WidthTiles = 640,
        HeightTiles = 220,
        CaveWalkerCount = 36,
        CaveWalkLength = 180,
        CopperVeinCount = 58,
        IronVeinCount = 40,
        TreeAttempts = 78,
        WaterPocketAttempts = 34,
        Ores = new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = 58,
                MinDepthOffset = 8,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = 40,
                MinDepthOffset = 16,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            }
        }
    };

    public static WorldGenerationProfile Large { get; } = Small with
    {
        Id = "large",
        WidthTiles = 960,
        HeightTiles = 300,
        CaveWalkerCount = 64,
        CaveWalkLength = 240,
        CopperVeinCount = 92,
        IronVeinCount = 70,
        TreeAttempts = 120,
        WaterPocketAttempts = 58,
        Ores = new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = 92,
                MinDepthOffset = 8,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = 70,
                MinDepthOffset = 16,
                Radius = 2,
                MinLength = 5,
                MaxLength = 12
            }
        }
    };

    public static WorldGenerationProfile ForDimensions(int widthTiles, int heightTiles)
    {
        return Small with
        {
            Id = "custom",
            WidthTiles = widthTiles,
            HeightTiles = heightTiles,
            SurfaceBaseY = Math.Clamp(heightTiles / 3, 6, Math.Max(6, heightTiles - 10)),
            SurfaceAmplitude = Math.Max(3, heightTiles / 10),
            DirtDepthMin = 4,
            DirtDepthMax = 8,
            CaveWalkerCount = Math.Max(4, widthTiles / 32),
            CaveWalkLength = Math.Max(50, heightTiles * 2),
            CopperVeinCount = Math.Max(8, widthTiles / 8),
            IronVeinCount = Math.Max(5, widthTiles / 12),
            TreeAttempts = Math.Max(8, widthTiles / 7),
            WaterPocketAttempts = Math.Max(2, widthTiles / 64),
            Ores = new[]
            {
                new OreGenerationDefinition
                {
                    TileId = KnownTileIds.CopperOre,
                    VeinCount = Math.Max(8, widthTiles / 8),
                    MinDepthOffset = 8,
                    Radius = 2
                },
                new OreGenerationDefinition
                {
                    TileId = KnownTileIds.IronOre,
                    VeinCount = Math.Max(5, widthTiles / 12),
                    MinDepthOffset = 16,
                    Radius = 2
                }
            }
        };
    }
}
