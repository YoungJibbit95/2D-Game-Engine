using Game.Core.Data;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationService
{
    private readonly AdvancedWorldGenerator _generator;
    private readonly WorldAnalyzer _analyzer;
    private readonly WorldGenerationQualityGate _qualityGate;

    public WorldGenerationService(
        AdvancedWorldGenerator? generator = null,
        WorldAnalyzer? analyzer = null,
        WorldGenerationQualityGate? qualityGate = null)
    {
        _generator = generator ?? new AdvancedWorldGenerator();
        _analyzer = analyzer ?? new WorldAnalyzer();
        _qualityGate = qualityGate ?? new WorldGenerationQualityGate();
    }

    public WorldGenerationBuildResult Generate(GameContentDatabase content, WorldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(request);

        var profile = ResolveProfile(content, request.ProfileId);
        profile = ApplyDimensionOverrides(profile, request.WidthTiles, request.HeightTiles);

        var generation = _generator.GenerateDetailed(profile, request.Seed);
        var analysis = _analyzer.Analyze(generation.World);
        var quality = _qualityGate.Evaluate(analysis, request.QualityRules);
        return new WorldGenerationBuildResult(profile, generation, analysis, quality);
    }

    private static WorldGenerationProfile ResolveProfile(GameContentDatabase content, string profileId)
    {
        var id = string.IsNullOrWhiteSpace(profileId) ? WorldGenerationProfile.Small.Id : profileId;

        if (content.WorldGenerationProfiles.TryGetById(id, out var contentProfile))
        {
            return contentProfile;
        }

        return id.ToLowerInvariant() switch
        {
            "small" => WorldGenerationProfile.Small,
            "medium" => WorldGenerationProfile.Medium,
            "large" => WorldGenerationProfile.Large,
            _ => throw new KeyNotFoundException($"World generation profile '{id}' was not found.")
        };
    }

    private static WorldGenerationProfile ApplyDimensionOverrides(
        WorldGenerationProfile profile,
        int? widthTiles,
        int? heightTiles)
    {
        if (widthTiles is null && heightTiles is null)
        {
            return profile;
        }

        var width = Math.Max(16, widthTiles ?? profile.WidthTiles);
        var height = Math.Max(16, heightTiles ?? profile.HeightTiles);
        return profile with
        {
            Id = profile.Id,
            WidthTiles = width,
            HeightTiles = height,
            SurfaceBaseY = Math.Clamp(profile.SurfaceBaseY, 6, Math.Max(6, height - 10))
        };
    }
}
