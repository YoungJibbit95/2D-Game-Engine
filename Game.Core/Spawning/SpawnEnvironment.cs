namespace Game.Core.Spawning;

public readonly record struct SpawnEnvironment(
    string? BiomeId = null,
    string? VerticalLayerId = null,
    string? WeatherId = null,
    string? WorldEventId = null,
    float DensityMultiplier = 1f,
    bool IsSpecified = true)
{
    public const string NoneId = "none";

    public static SpawnEnvironment Default { get; } = new(
        null,
        null,
        null,
        null,
        1f,
        true);
}
