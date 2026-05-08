namespace Game.Core.Entities.AI;

public interface IAiBehavior
{
    void Update(EnemyEntity entity, World.World world, float deltaSeconds);
}
