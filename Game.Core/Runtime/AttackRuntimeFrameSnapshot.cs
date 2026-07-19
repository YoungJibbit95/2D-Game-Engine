using Game.Core.Combat;

namespace Game.Core.Runtime;

public readonly record struct AttackRuntimeFrameSnapshot(
    string? ItemId,
    string? SequenceId,
    string? StepId,
    ulong AttackInstanceId,
    AttackRuntimePhase Phase,
    int ComboIndex,
    int ActivePhaseTick,
    bool HasQueuedCombo,
    bool HasBufferedInput,
    ImmutableSnapshotList<AttackRuntimeEvent> Feedback,
    int DroppedFeedback)
{
    public static AttackRuntimeFrameSnapshot Empty { get; } = new(
        null,
        null,
        null,
        0,
        AttackRuntimePhase.Idle,
        0,
        -1,
        false,
        false,
        ImmutableSnapshotList<AttackRuntimeEvent>.Empty,
        0);

    public bool HasSequence => !string.IsNullOrWhiteSpace(SequenceId);
}
