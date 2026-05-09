namespace Game.Core.Data;

public static class DefinitionTags
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool HasTag(IEnumerable<string> tags, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase));
    }
}
