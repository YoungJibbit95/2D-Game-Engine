using Game.Core.Physics;

namespace Game.Core.Characters;

public sealed class CharacterAnimationStateResolver
{
    public CharacterAnimationState Resolve(
        PhysicsBody body,
        bool isMining = false,
        bool isAttacking = false,
        bool isUsingItem = false,
        bool isHurt = false,
        bool isDead = false)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (isDead)
        {
            return CharacterAnimationState.Dead;
        }

        if (isHurt)
        {
            return CharacterAnimationState.Hurt;
        }

        if (isAttacking)
        {
            return CharacterAnimationState.Attack;
        }

        if (isMining)
        {
            return CharacterAnimationState.Mine;
        }

        if (isUsingItem)
        {
            return CharacterAnimationState.UseItem;
        }

        if (!body.OnGround)
        {
            return body.Velocity.Y < -5f ? CharacterAnimationState.Jump : CharacterAnimationState.Fall;
        }

        return Math.Abs(body.Velocity.X) > 5f ? CharacterAnimationState.Walk : CharacterAnimationState.Idle;
    }
}
