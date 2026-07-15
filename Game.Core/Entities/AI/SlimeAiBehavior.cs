using System.Numerics;

namespace Game.Core.Entities.AI;

public sealed class SlimeAiBehavior : IAiBehavior
{
    private const float HorizontalSpeed = 52f;
    private const float JumpVelocity = -255f;

    private float _decisionTimer;
    private int _direction = 1;

    public AiState CurrentState => AiState.Wander;

    public int? TargetEntityId => null;

    public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
    {
        _decisionTimer -= deltaSeconds;

        if (_decisionTimer <= 0)
        {
            _direction *= -1;
            _decisionTimer = 1.2f;
        }

        if (!entity.Body.OnGround)
        {
            return;
        }

        entity.Body.Velocity = new Vector2(HorizontalSpeed * _direction, JumpVelocity);
        entity.Body.OnGround = false;
    }

    public bool TryConsumeAttackIntent(out AiAttackIntent intent)
    {
        intent = default;
        return false;
    }
}
