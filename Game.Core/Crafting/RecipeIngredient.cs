namespace Game.Core.Crafting;

public sealed record RecipeIngredient
{
    public required string ItemId { get; init; }

    public required int Count { get; init; }
}
