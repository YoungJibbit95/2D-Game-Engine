using Game.Core.Data;

namespace Game.Core.Assets.Generation;

public sealed class SpriteGenerationBriefRegistry
{
    private readonly Dictionary<string, SpriteGenerationBrief> _bySpriteId;

    private SpriteGenerationBriefRegistry(IEnumerable<SpriteGenerationBrief> briefs)
    {
        _bySpriteId = new Dictionary<string, SpriteGenerationBrief>(StringComparer.OrdinalIgnoreCase);
        foreach (var brief in briefs)
        {
            AddValidated(brief);
        }
    }

    public IReadOnlyCollection<SpriteGenerationBrief> Briefs => _bySpriteId.Values;

    public static SpriteGenerationBriefRegistry Create(IEnumerable<SpriteGenerationBrief> briefs)
    {
        return new SpriteGenerationBriefRegistry(briefs);
    }

    public bool TryGetBySpriteId(string spriteId, out SpriteGenerationBrief brief)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        return _bySpriteId.TryGetValue(spriteId, out brief!);
    }

    public SpriteGenerationBrief GetBySpriteId(string spriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        return _bySpriteId.GetValueOrDefault(spriteId)
            ?? throw new KeyNotFoundException($"Sprite generation brief '{spriteId}' was not found.");
    }

    private void AddValidated(SpriteGenerationBrief brief)
    {
        ArgumentNullException.ThrowIfNull(brief);
        RequireText(brief.SpriteId, nameof(brief.SpriteId));
        RequireText(brief.OutputPath, nameof(brief.OutputPath));

        if (brief.Width <= 0 || brief.Height <= 0)
        {
            throw new RegistryValidationException($"Sprite generation brief '{brief.SpriteId}' has invalid dimensions.");
        }

        if (_bySpriteId.ContainsKey(brief.SpriteId))
        {
            throw new RegistryValidationException($"Duplicate sprite generation brief '{brief.SpriteId}'.");
        }

        _bySpriteId.Add(brief.SpriteId, brief);
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Sprite generation brief field '{name}' is required.");
        }
    }
}
