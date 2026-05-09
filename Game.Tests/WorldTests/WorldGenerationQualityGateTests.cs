using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationQualityGateTests
{
    [Fact]
    public void Evaluate_PassesReasonableGeneratedWorld()
    {
        var result = new AdvancedWorldGenerator().GenerateDetailed(WorldGenerationProfile.Small with
        {
            WidthTiles = 160,
            HeightTiles = 96
        }, seed: 123);
        var analysis = new WorldAnalyzer().Analyze(result.World);

        var report = new WorldGenerationQualityGate().Evaluate(analysis, new WorldGenerationQualityRules(MinSurfaceVariance: 0, MinLiquidTiles: 1));

        Assert.True(report.IsAcceptable, string.Join(Environment.NewLine, report.Issues));
    }

    [Fact]
    public void Evaluate_ReportsBadRatios()
    {
        var analysis = new WorldGenerationAnalysis(
            WidthTiles: 10,
            HeightTiles: 10,
            AirTileCount: 99,
            SolidTileCount: 1,
            LiquidTileCount: 0,
            NaturalTileCount: 1,
            MinSurfaceY: 5,
            MaxSurfaceY: 5,
            AverageSurfaceY: 5,
            TileCounts: new Dictionary<ushort, int>());

        var report = new WorldGenerationQualityGate().Evaluate(analysis);

        Assert.False(report.IsAcceptable);
        Assert.NotEmpty(report.Issues);
    }
}
