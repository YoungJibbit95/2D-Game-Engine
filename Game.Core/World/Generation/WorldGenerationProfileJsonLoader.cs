using System.Text.Json;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationProfileJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyList<WorldGenerationProfile> LoadProfilesFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<WorldGenerationProfile>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".region.json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadProfileFromFile)
            .ToArray();
    }

    public WorldGenerationProfile LoadProfileFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return LoadProfile(stream, filePath);
    }

    public WorldGenerationProfile LoadProfileFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadProfile(stream, "inline json");
    }

    private static WorldGenerationProfile LoadProfile(Stream stream, string source)
    {
        var profile = JsonSerializer.Deserialize<WorldGenerationProfile>(stream, Options);
        if (profile is null)
        {
            throw new JsonException($"World generation profile was empty: {source}");
        }

        var validationErrors = Validate(profile);
        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid world generation profile '{source}': {string.Join(" ", validationErrors)}");
        }

        return profile;
    }

    private static IReadOnlyList<string> Validate(WorldGenerationProfile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            errors.Add("Id is required.");
        }

        if (profile.WidthTiles < 16 || profile.HeightTiles < 20)
        {
            errors.Add("Dimensions must be at least 16x20 tiles.");
        }

        if (profile.SurfaceBaseY < 3 || profile.SurfaceBaseY >= profile.HeightTiles - 3)
        {
            errors.Add("SurfaceBaseY must leave at least three tiles above and below the surface.");
        }

        RequireNonNegative(errors, profile.SurfaceAmplitude, nameof(profile.SurfaceAmplitude));
        RequireRange(errors, profile.DirtDepthMin, profile.DirtDepthMax, 1, nameof(profile.DirtDepthMin), nameof(profile.DirtDepthMax));
        RequireNonNegative(errors, profile.CaveWalkerCount, nameof(profile.CaveWalkerCount));
        RequireNonNegative(errors, profile.CaveWalkLength, nameof(profile.CaveWalkLength));
        RequireNonNegative(errors, profile.CaveMinDepthOffset, nameof(profile.CaveMinDepthOffset));
        RequireNonNegative(errors, profile.CaveClampDepthOffset, nameof(profile.CaveClampDepthOffset));
        RequireRange(errors, profile.CaveMinRadius, profile.CaveMaxRadius, 1, nameof(profile.CaveMinRadius), nameof(profile.CaveMaxRadius));
        RequireChance(errors, profile.CaveRadiusChangeChance, nameof(profile.CaveRadiusChangeChance));

        RequireNonNegative(errors, profile.CavernRoomCount, nameof(profile.CavernRoomCount));
        RequireNonNegative(errors, profile.CavernMinDepthOffset, nameof(profile.CavernMinDepthOffset));
        RequireRange(errors, profile.CavernMinRadiusX, profile.CavernMaxRadiusX, 1, nameof(profile.CavernMinRadiusX), nameof(profile.CavernMaxRadiusX));
        RequireRange(errors, profile.CavernMinRadiusY, profile.CavernMaxRadiusY, 1, nameof(profile.CavernMinRadiusY), nameof(profile.CavernMaxRadiusY));
        RequireChance(errors, profile.CavernIrregularity, nameof(profile.CavernIrregularity));
        RequirePositive(errors, profile.CavernConnectorRadius, nameof(profile.CavernConnectorRadius));
        RequireNonNegative(errors, profile.CavernConnectorWander, nameof(profile.CavernConnectorWander));

        RequireNonNegative(errors, profile.WaterPocketAttempts, nameof(profile.WaterPocketAttempts));
        RequireNonNegative(errors, profile.WaterMinDepthOffset, nameof(profile.WaterMinDepthOffset));
        RequireRange(errors, profile.WaterMinRadiusX, profile.WaterMaxRadiusX, 1, nameof(profile.WaterMinRadiusX), nameof(profile.WaterMaxRadiusX));
        RequireRange(errors, profile.WaterMinRadiusY, profile.WaterMaxRadiusY, 1, nameof(profile.WaterMinRadiusY), nameof(profile.WaterMaxRadiusY));

        RequireNonNegative(errors, profile.SurfaceLakeAttempts, nameof(profile.SurfaceLakeAttempts));
        RequireRange(errors, profile.SurfaceLakeMinWidth, profile.SurfaceLakeMaxWidth, 5, nameof(profile.SurfaceLakeMinWidth), nameof(profile.SurfaceLakeMaxWidth));
        RequireRange(errors, profile.SurfaceLakeMinDepth, profile.SurfaceLakeMaxDepth, 1, nameof(profile.SurfaceLakeMinDepth), nameof(profile.SurfaceLakeMaxDepth));
        RequireNonNegative(errors, profile.SurfaceLakeMinSpacing, nameof(profile.SurfaceLakeMinSpacing));
        RequirePositive(errors, profile.SurfaceLakeShoreExponent, nameof(profile.SurfaceLakeShoreExponent));
        RequireChance(errors, profile.SurfaceLakeBottomIrregularity, nameof(profile.SurfaceLakeBottomIrregularity));

        RequireNonNegative(errors, profile.CavePoolAttempts, nameof(profile.CavePoolAttempts));
        RequireNonNegative(errors, profile.CavePoolMinDepthOffset, nameof(profile.CavePoolMinDepthOffset));
        RequireRange(errors, profile.CavePoolMinWidth, profile.CavePoolMaxWidth, 5, nameof(profile.CavePoolMinWidth), nameof(profile.CavePoolMaxWidth));
        RequireRange(errors, profile.CavePoolMinDepth, profile.CavePoolMaxDepth, 1, nameof(profile.CavePoolMinDepth), nameof(profile.CavePoolMaxDepth));
        RequirePositive(errors, profile.CavePoolBasinExponent, nameof(profile.CavePoolBasinExponent));
        RequireChance(errors, profile.CavePoolBottomIrregularity, nameof(profile.CavePoolBottomIrregularity));

        RequireNonNegative(errors, profile.UndergroundWallStartDepthOffset, nameof(profile.UndergroundWallStartDepthOffset));
        if (profile.DirtWallId == 0 || profile.StoneWallId == 0)
        {
            errors.Add("DirtWallId and StoneWallId must be non-zero.");
        }

        RequireChance(errors, profile.UndergroundWallCoverageChance, nameof(profile.UndergroundWallCoverageChance));
        RequireChance(errors, profile.CaveWallCoverageChance, nameof(profile.CaveWallCoverageChance));
        RequirePositive(errors, profile.WallPatchScale, nameof(profile.WallPatchScale));
        RequireNonNegative(errors, profile.CaveWallCleanupPasses, nameof(profile.CaveWallCleanupPasses));
        if (profile.CaveWallCleanupMinNeighbors is < 0 or > 8)
        {
            errors.Add("CaveWallCleanupMinNeighbors must be between 0 and 8.");
        }

        if (profile.CaveWallCoreOpenNeighborThreshold is < 0 or > 8)
        {
            errors.Add("CaveWallCoreOpenNeighborThreshold must be between 0 and 8.");
        }

        RequireNonNegative(errors, profile.TreeAttempts, nameof(profile.TreeAttempts));
        RequireChance(errors, profile.TreeAttemptChance, nameof(profile.TreeAttemptChance));
        RequireRange(errors, profile.TreeMinHeight, profile.TreeMaxHeight, 1, nameof(profile.TreeMinHeight), nameof(profile.TreeMaxHeight));
        RequireNonNegative(errors, profile.CopperVeinCount, nameof(profile.CopperVeinCount));
        RequireNonNegative(errors, profile.IronVeinCount, nameof(profile.IronVeinCount));

        foreach (var ore in profile.Ores)
        {
            if (ore.TileId == 0 || ore.ReplaceTileId == 0 || ore.VeinCount < 0 || ore.Radius <= 0)
            {
                errors.Add("Ore definitions require non-zero tile ids, non-negative vein counts, and positive radii.");
                break;
            }

            if (ore.MinLength <= 0 || ore.MaxLength < ore.MinLength || ore.MinDepthOffset < 0 ||
                (ore.MaxDepthOffset > 0 && ore.MaxDepthOffset < ore.MinDepthOffset))
            {
                errors.Add("Ore length and depth ranges are invalid.");
                break;
            }
        }

        foreach (var dimension in profile.Dimensions)
        {
            if (string.IsNullOrWhiteSpace(dimension.Id) || dimension.MinTileY < 0 ||
                dimension.MaxTileYInclusive < dimension.MinTileY || dimension.MaxTileYInclusive >= profile.HeightTiles)
            {
                errors.Add("Dimension bands require an id and must stay within the world height.");
                break;
            }
        }

        return errors;
    }

    private static void RequireNonNegative(List<string> errors, int value, string name)
    {
        if (value < 0)
        {
            errors.Add($"{name} must not be negative.");
        }
    }

    private static void RequirePositive(List<string> errors, int value, string name)
    {
        if (value <= 0)
        {
            errors.Add($"{name} must be positive.");
        }
    }

    private static void RequirePositive(List<string> errors, float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            errors.Add($"{name} must be a finite positive value.");
        }
    }

    private static void RequireChance(List<string> errors, float value, string name)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
        {
            errors.Add($"{name} must be between 0 and 1.");
        }
    }

    private static void RequireRange(
        List<string> errors,
        int minValue,
        int maxValue,
        int absoluteMinimum,
        string minName,
        string maxName)
    {
        if (minValue < absoluteMinimum || maxValue < minValue)
        {
            errors.Add($"{minName}/{maxName} must form an ordered range starting at {absoluteMinimum} or greater.");
        }
    }
}
