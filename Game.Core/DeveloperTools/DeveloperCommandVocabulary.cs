using Game.Core.Events;

namespace Game.Core.DeveloperTools;

public static class DeveloperCommandVocabulary
{
    public static IReadOnlyList<string> WeatherModes { get; } = Array.AsReadOnly(
        new[] { "status", "clear", "rain", "storm", "fog", "snow", "blizzard", "reset" });

    public static IReadOnlyList<string> SaveModes { get; } = Array.AsReadOnly(
        new[] { "quick", "full", "checkpoint" });

    public static IReadOnlyList<string> RenderingFeatures { get; } = Array.AsReadOnly(
        new[]
        {
            "lighting",
            "shadows",
            "reflections",
            "raytracing",
            "particles",
            "background",
            "weather",
            "postfx",
            "ui-debug"
        });

    public static IReadOnlyList<string> LightingDebugViews { get; } = Array.AsReadOnly(
        new[]
        {
            "overlay",
            "lightmap",
            "occluders",
            "rays",
            "skylight",
            "temporal",
            "reflections",
            "timings"
        });

    public static IReadOnlyList<string> GameRules { get; } = Array.AsReadOnly(
        new[]
        {
            "enemy-spawning",
            "time-flow",
            "weather",
            "mana-cost",
            "building-range",
            "mining-speed",
            "friendly-fire"
        });

    public static IReadOnlyList<string> GameRuleTargets { get; } = Prepend("list", GameRules);

    public static IReadOnlyList<string> PlayerResourceActions { get; } = Array.AsReadOnly(
        new[] { "status", "fill", "set", "add" });

    public static IReadOnlyList<string> EventTypeNames { get; } = DiscoverEventTypeNames();

    private static IReadOnlyList<string> ToggleRuleValues { get; } = Array.AsReadOnly(
        new[] { "off", "on", "toggle", "reset" });

    private static IReadOnlyList<string> MultiplierRuleValues { get; } = Array.AsReadOnly(
        new[] { "0", "0.5", "1", "2", "reset" });

    public static IReadOnlyList<string> GetGameRuleValueSuggestions(string? ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return Array.Empty<string>();
        }

        return ruleId.ToLowerInvariant() switch
        {
            "weather" or "friendly-fire" => ToggleRuleValues,
            "enemy-spawning" or "time-flow" or "mana-cost" or "building-range" or "mining-speed" =>
                MultiplierRuleValues,
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyList<string> Prepend(string value, IReadOnlyList<string> values)
    {
        var result = new string[values.Count + 1];
        result[0] = value;
        for (var index = 0; index < values.Count; index++)
        {
            result[index + 1] = values[index];
        }

        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<string> DiscoverEventTypeNames()
    {
        return typeof(IGameEvent).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IGameEvent).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
