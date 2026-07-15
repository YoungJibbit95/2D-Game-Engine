using Game.Core.Tiles;

namespace Game.Core.Interaction;

public static class MiningProgressCalculator
{
    public static float GetProgressPerSecond(
        TileDefinition tile,
        int toolPower,
        MiningTuning tuning,
        float miningSpeedMultiplier = 1f)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(tuning);
        tuning.Validate();
        if (!float.IsFinite(miningSpeedMultiplier) || miningSpeedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(miningSpeedMultiplier));
        }

        if (toolPower < tile.MiningPowerRequired)
        {
            return 0f;
        }

        var hardness = Math.Max(tuning.MinimumHardness, tile.Hardness);
        var powerAdvantage = Math.Max(0, toolPower - tile.MiningPowerRequired);
        var toolMultiplier = 1f + powerAdvantage / tuning.ToolPowerForDoubleSpeed;
        return tuning.BaseSpeedMultiplier * toolMultiplier * miningSpeedMultiplier / hardness;
    }

    public static int GetRequiredFixedTicks(
        TileDefinition tile,
        int toolPower,
        MiningTuning tuning,
        int fixedTicksPerSecond,
        float miningSpeedMultiplier = 1f)
    {
        if (fixedTicksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedTicksPerSecond));
        }

        var progressPerSecond = GetProgressPerSecond(tile, toolPower, tuning, miningSpeedMultiplier);
        return progressPerSecond <= 0f
            ? int.MaxValue
            : Math.Max(1, (int)Math.Ceiling(fixedTicksPerSecond / progressPerSecond));
    }
}
