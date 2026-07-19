using Game.Core.Data;

namespace Game.Core.Entities.AI;

public enum AiBehaviorKind
{
    None,
    Slime,
    Critter,
    Wander,
    Hostile
}

public enum AiActivityPeriod
{
    Any,
    Day,
    Night
}

public sealed record AiProfileDefinition
{
    public AiBehaviorKind Kind { get; init; }

    public float DetectionRange { get; init; } = 112f;

    public float LoseTargetRange { get; init; } = 176f;

    public float MoveSpeed { get; init; } = 48f;

    public float FleeSpeed { get; init; } = 82f;

    public float PatrolRadius { get; init; } = 96f;

    public float ReturnHomeDistance { get; init; }

    public float FlockRadius { get; init; }

    public float FlockWeight { get; init; }

    public int MinFlockSize { get; init; } = 2;

    public float AttackRange { get; init; } = 22f;

    public float AttackCooldown { get; init; } = 0.8f;

    public float PerceptionMemorySeconds { get; init; } = 2.5f;

    public float FleeHealthThreshold { get; init; }

    public float JumpSpeed { get; init; } = 245f;

    public float DecisionInterval { get; init; } = 1.1f;

    public float IdleChance { get; init; } = 0.25f;

    public float DayMoveSpeedMultiplier { get; init; } = 1f;

    public float NightMoveSpeedMultiplier { get; init; } = 1f;

    public AiActivityPeriod ActivityPeriod { get; init; }

    public bool PerchWhenInactive { get; init; } = true;

    public bool RequiresLineOfSight { get; init; } = true;

    public bool AvoidLedges { get; init; } = true;

    public bool AvoidLiquid { get; init; } = true;

    public static void Validate(string entityId, AiProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!float.IsFinite(profile.DetectionRange) ||
            !float.IsFinite(profile.LoseTargetRange) ||
            profile.DetectionRange < 0 ||
            profile.LoseTargetRange < profile.DetectionRange)
        {
            throw new RegistryValidationException($"Entity '{entityId}' has invalid AI detection ranges.");
        }

        if (!float.IsFinite(profile.MoveSpeed) ||
            !float.IsFinite(profile.FleeSpeed) ||
            !float.IsFinite(profile.PatrolRadius) ||
            !float.IsFinite(profile.ReturnHomeDistance) ||
            !float.IsFinite(profile.FlockRadius) ||
            !float.IsFinite(profile.FlockWeight) ||
            !float.IsFinite(profile.AttackRange) ||
            !float.IsFinite(profile.JumpSpeed) ||
            profile.MoveSpeed < 0 ||
            profile.FleeSpeed < 0 ||
            profile.PatrolRadius < 0 ||
            profile.ReturnHomeDistance < 0 ||
            profile.FlockRadius < 0 ||
            profile.FlockWeight < 0 ||
            profile.AttackRange < 0 ||
            profile.JumpSpeed < 0)
        {
            throw new RegistryValidationException($"Entity '{entityId}' has negative AI steering values.");
        }

        if (profile.MinFlockSize < 1)
        {
            throw new RegistryValidationException($"Entity '{entityId}' AI minimum flock size must be positive.");
        }

        if (profile.FlockRadius > 0 && profile.FlockWeight > 0 && profile.MinFlockSize < 2)
        {
            throw new RegistryValidationException(
                $"Entity '{entityId}' AI minimum flock size must include at least one ally.");
        }

        if (!float.IsFinite(profile.AttackCooldown) ||
            !float.IsFinite(profile.DecisionInterval) ||
            !float.IsFinite(profile.PerceptionMemorySeconds) ||
            profile.AttackCooldown < 0 ||
            profile.DecisionInterval <= 0 ||
            profile.PerceptionMemorySeconds < 0)
        {
            throw new RegistryValidationException($"Entity '{entityId}' has invalid AI timing values.");
        }

        if (!float.IsFinite(profile.IdleChance) || profile.IdleChance < 0 || profile.IdleChance > 1)
        {
            throw new RegistryValidationException($"Entity '{entityId}' AI idle chance must be in the range 0..1.");
        }

        if (!float.IsFinite(profile.DayMoveSpeedMultiplier) ||
            !float.IsFinite(profile.NightMoveSpeedMultiplier) ||
            profile.DayMoveSpeedMultiplier < 0 ||
            profile.NightMoveSpeedMultiplier < 0)
        {
            throw new RegistryValidationException($"Entity '{entityId}' has invalid AI day/night speed multipliers.");
        }

        if (!float.IsFinite(profile.FleeHealthThreshold) ||
            profile.FleeHealthThreshold < 0 ||
            profile.FleeHealthThreshold > 1)
        {
            throw new RegistryValidationException($"Entity '{entityId}' AI flee health threshold must be in the range 0..1.");
        }
    }
}
