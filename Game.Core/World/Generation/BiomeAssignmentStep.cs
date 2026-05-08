namespace Game.Core.World.Generation;

public sealed class BiomeAssignmentStep : IWorldGenerationStep
{
    public string Name => "biomes";

    public int Order => -10;

    public void Apply(WorldGenerationContext context)
    {
        context.Biomes.AddRegion(0, context.World.WidthTiles - 1, "forest");
    }
}
