namespace Game.Core.Characters;

public sealed record CharacterAppearance
{
    public string BodySpriteId { get; init; } = "entities/player/base_actions";

    public string SkinTone { get; init; } = "#c98f62";

    public string HairStyleId { get; init; } = "short";

    public string HairColor { get; init; } = "#5b3825";

    public string ShirtColor { get; init; } = "#4f7fb8";

    public string PantsColor { get; init; } = "#2f3f5f";

    public string EyeColor { get; init; } = "#243043";
}
