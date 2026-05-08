namespace Game.Core.World.Generation;

public interface IWorldGenerationStep
{
    string Name { get; }

    int Order { get; }

    void Apply(WorldGenerationContext context);
}
