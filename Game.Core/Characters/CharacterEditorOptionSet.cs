namespace Game.Core.Characters;

public sealed record CharacterEditorOptionSet
{
    public IReadOnlyList<string> SkinTones { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> HairStyles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ClothesStyles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Accessories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> HairColors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ShirtColors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PantsColors { get; init; } = Array.Empty<string>();

    public static CharacterEditorOptionSet CreateDefault()
    {
        return new CharacterEditorOptionSet
        {
            SkinTones = new[] { "#8f563b", "#c98f62", "#e0b687", "#f0cfa3" },
            HairStyles = new[] { "short", "messy", "bob", "ponytail", "braids", "mohawk", "long", "cap" },
            ClothesStyles = new[] { "blue_jeans", "green_work", "red_trail", "violet_mage", "tan_worker", "grey_miner", "teal_scout", "ochre_farmer" },
            Accessories = new[] { "none", "straw_hat", "miner_helmet", "red_bandana", "glasses", "leaf_crown", "hood" },
            HairColors = new[] { "#2d1b14", "#5b3825", "#9d6735", "#d7b56d" },
            ShirtColors = new[] { "#4f7fb8", "#5b9b62", "#b85c4f", "#8d65b8" },
            PantsColors = new[] { "#2f3f5f", "#51443b", "#384f3a", "#5a384f" }
        };
    }
}
