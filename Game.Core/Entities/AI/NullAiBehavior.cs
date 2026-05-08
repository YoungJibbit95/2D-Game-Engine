namespace Game.Core.Entities.AI;

public sealed class NullAiBehavior : IAiBehavior
{
    public static NullAiBehavior Instance { get; } = new();

    private NullAiBehavior()
    {
    }

    public void Update(EnemyEntity entity, World.World world, float deltaSeconds)
    {
    }
}
