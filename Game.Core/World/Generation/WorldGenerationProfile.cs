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

    public int CavernRoomCount { get; init; }

    public int CavernMinDepthOffset { get; init; } = 18;

    public int CavernMinRadiusX { get; init; } = 8;

    public int CavernMaxRadiusX { get; init; } = 16;

    public int CavernMinRadiusY { get; init; } = 5;

    public int CavernMaxRadiusY { get; init; } = 9;

    public float CavernIrregularity { get; init; } = 0.22f;

    public int CavernConnectorRadius { get; init; } = 2;

    public int CavernConnectorWander { get; init; } = 8;

    public int WaterMinDepthOffset { get; init; } = 12;

    public int WaterMinRadiusX { get; init; } = 3;

    public int WaterMaxRadiusX { get; init; } = 7;

    public int WaterMinRadiusY { get; init; } = 2;

    public int WaterMaxRadiusY { get; init; } = 4;

    public int SurfaceLakeAttempts { get; init; }

    public int SurfaceLakeMinWidth { get; init; } = 14;

    public int SurfaceLakeMaxWidth { get; init; } = 28;

    public int SurfaceLakeMinDepth { get; init; } = 3;

    public int SurfaceLakeMaxDepth { get; init; } = 7;

    public int SurfaceLakeMinSpacing { get; init; } = 10;

    public float SurfaceLakeShoreExponent { get; init; } = 1.35f;

    public float SurfaceLakeBottomIrregularity { get; init; } = 0.18f;

    public int CavePoolAttempts { get; init; }

    public int CavePoolMinDepthOffset { get; init; } = 14;

    public int CavePoolMinWidth { get; init; } = 8;

    public int CavePoolMaxWidth { get; init; } = 18;

    public int CavePoolMinDepth { get; init; } = 2;

    public int CavePoolMaxDepth { get; init; } = 5;

    public float CavePoolBasinExponent { get; init; } = 0.75f;

    public float CavePoolBottomIrregularity { get; init; } = 0.22f;

    public int UndergroundWallStartDepthOffset { get; init; } = 3;

    public ushort DirtWallId { get; init; } = 1;

    public ushort StoneWallId { get; init; } = 2;

    public float UndergroundWallCoverageChance { get; init; } = 0.96f;

    public float CaveWallCoverageChance { get; init; } = 0.84f;

    public float WallPatchScale { get; init; } = 1f;

    public int CaveWallCleanupPasses { get; init; } = 2;

    public int CaveWallCleanupMinNeighbors { get; init; } = 2;

    public int CaveWallCoreOpenNeighborThreshold { get; init; } = 7;

    public float TreeAttemptChance { get; init; } = 0.55f;

    public int TreeMinHeight { get; init; } = 7;

    public int TreeMaxHeight { get; init; } = 10;

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
        CavernRoomCount = 4,
        CavernMinDepthOffset = 18,
        CavernMinRadiusX = 7,
        CavernMaxRadiusX = 13,
        CavernMinRadiusY = 4,
        CavernMaxRadiusY = 7,
        CavernConnectorWander = 6,
        CopperVeinCount = 30,
        IronVeinCount = 20,
        TreeAttempts = 44,
        TreeMinHeight = 7,
        TreeMaxHeight = 10,
        WaterPocketAttempts = 18,
        SurfaceLakeAttempts = 3,
        CavePoolAttempts = 10,
        SurfaceLakeMinWidth = 12,
        SurfaceLakeMaxWidth = 22,
        SurfaceLakeMinDepth = 3,
        SurfaceLakeMaxDepth = 6,
        SurfaceLakeMinSpacing = 10,
        SurfaceLakeShoreExponent = 1.35f,
        SurfaceLakeBottomIrregularity = 0.18f,
        CavePoolMinDepthOffset = 14,
        CavePoolMinWidth = 7,
        CavePoolMaxWidth = 14,
        CavePoolMinDepth = 2,
        CavePoolMaxDepth = 4,
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
        SurfaceBaseY = 72,
        SurfaceAmplitude = 14,
        DirtDepthMin = 10,
        DirtDepthMax = 22,
        CaveWalkerCount = 36,
        CaveWalkLength = 180,
        CaveMinDepthOffset = 9,
        CaveClampDepthOffset = 6,
        CaveMinRadius = 1,
        CaveMaxRadius = 3,
        CaveRadiusChangeChance = 0.1f,
        CavernRoomCount = 8,
        CavernMinDepthOffset = 22,
        CavernMinRadiusX = 9,
        CavernMaxRadiusX = 18,
        CavernMinRadiusY = 5,
        CavernMaxRadiusY = 10,
        CavernIrregularity = 0.26f,
        CavernConnectorWander = 9,
        CopperVeinCount = 58,
        IronVeinCount = 40,
        TreeAttempts = 78,
        TreeAttemptChance = 0.58f,
        TreeMinHeight = 8,
        TreeMaxHeight = 11,
        WaterPocketAttempts = 34,
        WaterMinDepthOffset = 14,
        WaterMinRadiusX = 4,
        WaterMaxRadiusX = 9,
        WaterMinRadiusY = 2,
        WaterMaxRadiusY = 5,
        SurfaceLakeAttempts = 5,
        CavePoolAttempts = 20,
        SurfaceLakeMinWidth = 14,
        SurfaceLakeMaxWidth = 30,
        SurfaceLakeMinDepth = 3,
        SurfaceLakeMaxDepth = 8,
        SurfaceLakeMinSpacing = 14,
        SurfaceLakeShoreExponent = 1.4f,
        SurfaceLakeBottomIrregularity = 0.22f,
        CavePoolMinDepthOffset = 16,
        CavePoolMinWidth = 8,
        CavePoolMaxWidth = 19,
        CavePoolMinDepth = 2,
        CavePoolMaxDepth = 5,
        CavePoolBasinExponent = 0.72f,
        CavePoolBottomIrregularity = 0.26f,
        UndergroundWallCoverageChance = 0.97f,
        CaveWallCoverageChance = 0.86f,
        WallPatchScale = 0.9f,
        Ores = new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = 58,
                MinDepthOffset = 8,
                Radius = 2,
                MinLength = 5,
                MaxLength = 14
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = 40,
                MinDepthOffset = 18,
                Radius = 2,
                MinLength = 5,
                MaxLength = 14
            }
        }
    };

    public static WorldGenerationProfile Large { get; } = Small with
    {
        Id = "large",
        WidthTiles = 960,
        HeightTiles = 300,
        SurfaceBaseY = 90,
        SurfaceAmplitude = 18,
        DirtDepthMin = 12,
        DirtDepthMax = 26,
        CaveWalkerCount = 64,
        CaveWalkLength = 240,
        CaveMinDepthOffset = 10,
        CaveClampDepthOffset = 8,
        CaveMinRadius = 1,
        CaveMaxRadius = 4,
        CaveRadiusChangeChance = 0.12f,
        CavernRoomCount = 14,
        CavernMinDepthOffset = 26,
        CavernMinRadiusX = 11,
        CavernMaxRadiusX = 24,
        CavernMinRadiusY = 6,
        CavernMaxRadiusY = 13,
        CavernIrregularity = 0.3f,
        CavernConnectorRadius = 3,
        CavernConnectorWander = 12,
        CopperVeinCount = 92,
        IronVeinCount = 70,
        TreeAttempts = 120,
        TreeAttemptChance = 0.6f,
        TreeMinHeight = 8,
        TreeMaxHeight = 12,
        WaterPocketAttempts = 58,
        WaterMinDepthOffset = 16,
        WaterMinRadiusX = 4,
        WaterMaxRadiusX = 11,
        WaterMinRadiusY = 2,
        WaterMaxRadiusY = 6,
        SurfaceLakeAttempts = 8,
        CavePoolAttempts = 34,
        SurfaceLakeMinWidth = 16,
        SurfaceLakeMaxWidth = 38,
        SurfaceLakeMinDepth = 4,
        SurfaceLakeMaxDepth = 10,
        SurfaceLakeMinSpacing = 18,
        SurfaceLakeShoreExponent = 1.45f,
        SurfaceLakeBottomIrregularity = 0.25f,
        CavePoolMinDepthOffset = 18,
        CavePoolMinWidth = 9,
        CavePoolMaxWidth = 24,
        CavePoolMinDepth = 2,
        CavePoolMaxDepth = 7,
        CavePoolBasinExponent = 0.7f,
        CavePoolBottomIrregularity = 0.3f,
        UndergroundWallStartDepthOffset = 4,
        UndergroundWallCoverageChance = 0.98f,
        CaveWallCoverageChance = 0.88f,
        WallPatchScale = 0.8f,
        CaveWallCleanupPasses = 3,
        Ores = new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = 92,
                MinDepthOffset = 8,
                Radius = 2,
                MinLength = 6,
                MaxLength = 16
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = 70,
                MinDepthOffset = 20,
                Radius = 3,
                MinLength = 6,
                MaxLength = 16
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
            CavernRoomCount = Math.Max(1, widthTiles / 96),
            CavernMinDepthOffset = Math.Max(10, heightTiles / 8),
            CavernMinRadiusX = Math.Clamp(widthTiles / 32, 4, 10),
            CavernMaxRadiusX = Math.Clamp(widthTiles / 18, 7, 18),
            CavernMinRadiusY = Math.Clamp(heightTiles / 24, 3, 7),
            CavernMaxRadiusY = Math.Clamp(heightTiles / 14, 5, 11),
            CavernConnectorWander = Math.Clamp(widthTiles / 48, 3, 10),
            CopperVeinCount = Math.Max(8, widthTiles / 8),
            IronVeinCount = Math.Max(5, widthTiles / 12),
            TreeAttempts = Math.Max(8, widthTiles / 7),
            WaterPocketAttempts = Math.Max(2, widthTiles / 64),
            SurfaceLakeAttempts = Math.Max(1, widthTiles / 160),
            CavePoolAttempts = Math.Max(1, widthTiles / 48),
            SurfaceLakeMinWidth = Math.Clamp(widthTiles / 16, 8, 14),
            SurfaceLakeMaxWidth = Math.Clamp(widthTiles / 9, 12, 28),
            SurfaceLakeMinDepth = Math.Clamp(heightTiles / 32, 2, 4),
            SurfaceLakeMaxDepth = Math.Clamp(heightTiles / 18, 4, 8),
            CavePoolMinWidth = Math.Clamp(widthTiles / 24, 5, 9),
            CavePoolMaxWidth = Math.Clamp(widthTiles / 12, 8, 18),
            CavePoolMinDepth = 2,
            CavePoolMaxDepth = Math.Clamp(heightTiles / 24, 3, 6),
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
