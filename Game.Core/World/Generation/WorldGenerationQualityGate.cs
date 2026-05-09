namespace Game.Core.World.Generation;

public sealed class WorldGenerationQualityGate
{
    public WorldGenerationQualityReport Evaluate(WorldGenerationAnalysis analysis, WorldGenerationQualityRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        rules ??= new WorldGenerationQualityRules();

        var issues = new List<string>();
        var total = Math.Max(1, analysis.WidthTiles * analysis.HeightTiles);
        var solidRatio = analysis.SolidTileCount / (float)total;
        var airRatio = analysis.AirTileCount / (float)total;
        var surfaceVariance = analysis.MaxSurfaceY - analysis.MinSurfaceY;

        if (solidRatio < rules.MinSolidRatio)
        {
            issues.Add($"Solid ratio {solidRatio:0.00} is below {rules.MinSolidRatio:0.00}.");
        }

        if (solidRatio > rules.MaxSolidRatio)
        {
            issues.Add($"Solid ratio {solidRatio:0.00} is above {rules.MaxSolidRatio:0.00}.");
        }

        if (airRatio < rules.MinAirRatio)
        {
            issues.Add($"Air ratio {airRatio:0.00} is below {rules.MinAirRatio:0.00}.");
        }

        if (surfaceVariance < rules.MinSurfaceVariance)
        {
            issues.Add($"Surface variance {surfaceVariance} is below {rules.MinSurfaceVariance}.");
        }

        if (analysis.LiquidTileCount < rules.MinLiquidTiles)
        {
            issues.Add($"Liquid tile count {analysis.LiquidTileCount} is below {rules.MinLiquidTiles}.");
        }

        return issues.Count == 0
            ? WorldGenerationQualityReport.Pass
            : new WorldGenerationQualityReport(false, issues);
    }
}
