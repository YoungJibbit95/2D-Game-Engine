using Game.Core.Animation;

namespace Game.Tests.AnimationTests;

internal static class AnimationTestFactory
{
    public static AnimationClip CreateClip(
        string id,
        AnimationLoopMode loopMode = AnimationLoopMode.Loop,
        IReadOnlyList<int>? frameDurations = null,
        IReadOnlyList<AnimationEventMarker>? events = null,
        string trackId = "body",
        string spriteId = "character/body")
    {
        frameDurations ??= new[] { 1, 1, 1 };
        var frames = new AnimationFrame[frameDurations.Count];
        for (var index = 0; index < frameDurations.Count; index++)
        {
            frames[index] = new AnimationFrame(index, frameDurations[index]);
        }

        return new AnimationClip(
            id,
            loopMode,
            new[] { new AnimationTrack(trackId, trackId, spriteId, frames) },
            events);
    }

    public static AnimationStateLayerDefinition CreateLayer(
        string id,
        string initialStateId,
        IReadOnlyList<AnimationStateDefinition> states,
        IReadOnlyList<AnimationTransitionDefinition>? transitions = null,
        int priority = 0)
    {
        return new AnimationStateLayerDefinition(id, priority, initialStateId, states, transitions);
    }

    public static LayeredAnimationStateMachine CreateMachine(AnimationStateLayerDefinition layer)
    {
        return new LayeredAnimationStateMachine(new LayeredAnimationStateMachineDefinition(new[] { layer }));
    }
}
