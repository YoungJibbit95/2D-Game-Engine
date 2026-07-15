namespace Game.Core.Entities.AI;

public readonly record struct AiAttackIntent(
    int AttackerEntityId,
    int TargetEntityId,
    int Damage,
    float Range,
    float Knockback);
