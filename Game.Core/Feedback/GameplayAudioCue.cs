using Game.Core.Audio;
using System.Numerics;

namespace Game.Core.Feedback;

public readonly record struct GameplayAudioCue(
    GameplayFeedbackCueKind SourceKind,
    string AudioId,
    AudioBus Bus,
    Vector2 WorldPosition,
    float Volume = 1f,
    float Pitch = 0f,
    int Priority = 50,
    float CooldownSeconds = 0f,
    bool IsSpatial = true,
    float MaximumDistance = 640f);

public readonly record struct GameplayFeedbackQueueTelemetry(
    int PendingVisualCommands,
    int PendingAudioCommands,
    long VisualCommandsEnqueued,
    long VisualCommandsDropped,
    long VisualCommandsDrained,
    long AudioCommandsEnqueued,
    long AudioCommandsDropped,
    long AudioCommandsDrained);
