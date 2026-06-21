namespace Game.Core.Characters;

public sealed record CharacterAnimationSetDefinition
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? DefaultClipId { get; init; }

    public IReadOnlyDictionary<CharacterAnimationState, string> StateClips { get; init; } =
        new Dictionary<CharacterAnimationState, string>();

    public string? ResolveClipId(CharacterAnimationState state)
    {
        if (StateClips.TryGetValue(state, out var clipId))
        {
            return clipId;
        }

        if (StateClips.TryGetValue(CharacterAnimationState.Idle, out var idleClipId))
        {
            return idleClipId;
        }

        return DefaultClipId;
    }
}
