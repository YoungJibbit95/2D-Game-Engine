namespace Game.Core.Entities.AI;

public sealed class NullAiBehavior : IAiBehavior
{
    public static NullAiBehavior Instance { get; } = new();

    private NullAiBehavior()
    {
    }

    public AiState CurrentState => AiState.Idle;

    public int? TargetEntityId => null;

    public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
    {
    }

    public bool TryConsumeAttackIntent(out AiAttackIntent intent)
    {
        intent = default;
        return false;
    }
}
