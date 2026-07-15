namespace Game.Core.Entities.AI;

public interface IAiBehavior
{
    AiState CurrentState { get; }

    int? TargetEntityId { get; }

    AiTelemetrySnapshot Telemetry => AiTelemetrySnapshot.Empty(CurrentState, TargetEntityId);

    void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds);

    bool TryConsumeAttackIntent(out AiAttackIntent intent);
}
