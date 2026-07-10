namespace Game.Core.World.Generation;

public sealed class WaterPocketGenerationStep : IWorldGenerationStep
{
    public string Name => "water";

    public int Order => 16;

    public void Apply(WorldGenerationContext context)
    {
        new CavePoolGenerationStep(context.Profile.WaterPocketAttempts).Apply(context);
    }
}
