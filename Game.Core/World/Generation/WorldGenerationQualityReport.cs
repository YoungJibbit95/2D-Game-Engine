namespace Game.Core.World.Generation;

public sealed record WorldGenerationQualityReport(bool IsAcceptable, IReadOnlyList<string> Issues)
{
    public static WorldGenerationQualityReport Pass { get; } = new(true, Array.Empty<string>());
}
