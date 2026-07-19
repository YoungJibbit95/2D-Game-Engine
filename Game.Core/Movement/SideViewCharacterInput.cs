namespace Game.Core.Movement;

/// <summary>
/// Renderer- and entity-neutral control intent consumed by a side-view character controller.
/// </summary>
public readonly record struct SideViewCharacterInput(float MoveAxis, bool WantsJump)
{
    public static SideViewCharacterInput None => default;
}
