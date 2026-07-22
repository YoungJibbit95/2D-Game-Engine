namespace Game.Core.Movement;

/// <summary>
/// Renderer- and entity-neutral control intent consumed by a side-view character controller.
/// </summary>
public readonly record struct SideViewCharacterInput(
    float MoveAxis,
    bool WantsJump = false,
    bool WantsFly = false,
    bool WantsGlide = false,
    bool CanDoubleJump = false,
    bool CanWallJump = false,
    bool CanFly = false,
    bool CanGlide = false)
{
    public static SideViewCharacterInput None => default;
}
