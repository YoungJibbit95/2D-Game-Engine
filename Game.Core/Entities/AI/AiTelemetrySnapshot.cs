using System.Numerics;

namespace Game.Core.Entities.AI;

public readonly record struct AiTelemetrySnapshot(
    AiState State,
    int? TargetEntityId,
    Vector2 HomePosition,
    Vector2 LastKnownTargetPosition,
    float MemoryRemainingSeconds,
    bool TargetVisible,
    bool ActivePeriod,
    int NearbyAllies,
    long UpdateCount,
    long DecisionCount,
    long StateTransitionCount)
{
    public static AiTelemetrySnapshot Empty(AiState state, int? targetEntityId)
    {
        return new AiTelemetrySnapshot(
            state,
            targetEntityId,
            default,
            default,
            0,
            false,
            true,
            0,
            0,
            0,
            0);
    }
}
