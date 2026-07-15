namespace Game.Core.Lighting;

/// <summary>
/// Describes whether direct sky light can be resolved from currently materialized world data.
/// </summary>
public enum SkyExposureState : byte
{
    Open = 0,
    Occluded = 1,
    Unknown = 2
}
