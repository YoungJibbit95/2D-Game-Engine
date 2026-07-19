using Game.Core.Physics;

namespace Game.Core.Entities;

internal static class EntityPhysicsRuntime
{
    public const float Gravity = 1_050f;
    public const float DroppedItemGravityScale = 850f / Gravity;
    public const int DefaultMaximumBodies = 4_096;
    public const int DefaultMaximumBodyPairs = 131_072;
    public const int ContactsPerBody = 4;

    public static PhysicsStepSettings CreateSettings(int maximumBodies)
    {
        if (maximumBodies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBodies));
        }

        return new PhysicsStepSettings(
            new System.Numerics.Vector2(0f, Gravity),
            1f,
            4_096f,
            maximumBodies,
            ContactsPerBody);
    }

    public static PhysicsBroadphaseSettings CreateBroadphaseSettings(
        int maximumBodies,
        int maximumBodyPairs)
    {
        if (maximumBodies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBodies));
        }

        if (maximumBodyPairs <= 0 || maximumBodyPairs > int.MaxValue / 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBodyPairs));
        }

        return new PhysicsBroadphaseSettings(
            maximumBodies,
            Math.Max(
                PhysicsBroadphaseSettings.Default.MaximumPairTests,
                checked(maximumBodyPairs * 2)),
            false);
    }
}

internal interface IEntityPhysicsParticipant
{
    PhysicsBody Body { get; }

    TileCollisionSettings CollisionSettings { get; }

    void SynchronizePhysicsState();
}
