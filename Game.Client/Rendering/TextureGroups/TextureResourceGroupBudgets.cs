namespace Game.Client.Rendering.TextureGroups;

public sealed class TextureResourceGroupBudgets
{
    private readonly long[] _decodedBytes;

    public TextureResourceGroupBudgets(
        long uiDecodedBytes = long.MaxValue,
        long worldDecodedBytes = long.MaxValue,
        long entitiesDecodedBytes = long.MaxValue,
        long backgroundsDecodedBytes = long.MaxValue,
        long effectsDecodedBytes = long.MaxValue)
    {
        _decodedBytes =
        [
            Validate(uiDecodedBytes, nameof(uiDecodedBytes)),
            Validate(worldDecodedBytes, nameof(worldDecodedBytes)),
            Validate(entitiesDecodedBytes, nameof(entitiesDecodedBytes)),
            Validate(backgroundsDecodedBytes, nameof(backgroundsDecodedBytes)),
            Validate(effectsDecodedBytes, nameof(effectsDecodedBytes))
        ];
    }

    public long this[TextureResourceGroup group]
    {
        get
        {
            ValidateGroup(group);
            return _decodedBytes[(int)group];
        }
    }

    private static long Validate(long value, string parameterName)
    {
        return value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Decoded-byte budgets cannot be negative.");
    }

    private static void ValidateGroup(TextureResourceGroup group)
    {
        if ((uint)group >= TextureResourceGroups.Ordered.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown texture resource group.");
        }
    }
}
