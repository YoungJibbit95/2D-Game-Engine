
using Game.Core.DeveloperTools;
using System.Globalization;
using System.Numerics;

namespace Game.Core.Commands;

public sealed class WeatherCommand : TypedConsoleCommand
{
    public WeatherCommand()
        : base(new CommandSpecification(
            "weather",
            "Reads or requests a biome-local weather override.",
            new[]
            {
                new CommandArgumentSpecification(
                    "mode",
                    CommandArgumentType.Choice,
                    false,
                    "Weather preset, status, or reset.",
                    DeveloperCommandVocabulary.WeatherModes),
                new CommandArgumentSpecification(
                    "intensity",
                    CommandArgumentType.Number,
                    false,
                    "Normalized weather intensity.",
                    minimum: 0,
                    maximum: 1)
            },
            aliases: new[] { "climate" },
            examples: new[] { "/weather", "/weather clear", "/weather snow 0.65", "/weather reset" },
            category: CommandCategory.Environment,
            searchTerms: new[] { "rain", "snow", "storm", "fog", "biome", "climate" },
            requestIntentType: typeof(WeatherOverrideRequestIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var mode = arguments.GetOptionalString("mode") ?? "status";
        if (mode.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Has("intensity"))
            {
                return CommandResult.Failure("too_many_arguments", "/weather status does not accept intensity.");
            }

            return CommandResult.Request(
                "weather_status_requested",
                "Requested current biome weather status.",
                new WeatherOverrideRequestIntent(WeatherOverrideRequestKind.Status, null, null));
        }

        if (mode.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Has("intensity"))
            {
                return CommandResult.Failure("too_many_arguments", "/weather reset does not accept intensity.");
            }

            return CommandResult.Request(
                "weather_reset_requested",
                "Requested deterministic biome weather.",
                new WeatherOverrideRequestIntent(WeatherOverrideRequestKind.Reset, null, null));
        }

        var intensity = arguments.Has("intensity") ? arguments.GetSingle("intensity") : 1f;
        return CommandResult.Request(
            "weather_override_requested",
            $"Requested {mode} weather at {intensity:0.##} intensity.",
            new WeatherOverrideRequestIntent(WeatherOverrideRequestKind.Set, mode, intensity));
    }
}

public sealed class BiomeCommand : TypedConsoleCommand
{
    public BiomeCommand()
        : base(new CommandSpecification(
            "biome",
            "Reads or requests a local biome override.",
            new[]
            {
                new CommandArgumentSpecification(
                    "biomeId",
                    CommandArgumentType.Identifier,
                    false,
                    "Registered biome id, status, or reset.",
                    choices: new[] { "status", "reset" },
                    suggestionSource: CommandSuggestionSource.Biomes)
            },
            examples: new[] { "/biome", "/biome forest", "/biome reset" },
            category: CommandCategory.Environment,
            searchTerms: new[] { "region", "environment", "override" },
            requestIntentType: typeof(BiomeOverrideRequestIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var biomeId = arguments.GetOptionalString("biomeId") ?? "status";
        if (biomeId.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Request(
                "biome_status_requested",
                "Requested current biome status.",
                new BiomeOverrideRequestIntent(BiomeOverrideRequestKind.Status, null));
        }

        if (biomeId.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.Request(
                "biome_reset_requested",
                "Requested removal of the local biome override.",
                new BiomeOverrideRequestIntent(BiomeOverrideRequestKind.Reset, null));
        }

        if (context.Content is not null && !context.Content.Biomes.TryGetById(biomeId, out _))
        {
            return CommandResult.Failure("unknown_biome", $"Unknown biome '{biomeId}'.");
        }

        return CommandResult.Request(
            "biome_override_requested",
            $"Requested local biome override '{biomeId}'.",
            new BiomeOverrideRequestIntent(BiomeOverrideRequestKind.Set, biomeId));
    }
}

public sealed class DeveloperSaveCommand : TypedConsoleCommand
{
    public DeveloperSaveCommand()
        : base(new CommandSpecification(
            "save",
            "Requests an authoritative save operation.",
            new[]
            {
                new CommandArgumentSpecification(
                    "mode",
                    CommandArgumentType.Choice,
                    false,
                    "Save mode.",
                    DeveloperCommandVocabulary.SaveModes)
            },
            aliases: new[] { "quicksave" },
            examples: new[] { "/save", "/save full", "/save checkpoint" },
            category: CommandCategory.Persistence,
            searchTerms: new[] { "world", "player", "checkpoint", "disk" },
            requestIntentType: typeof(DeveloperSaveRequestIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var modeText = arguments.GetOptionalString("mode") ?? "quick";
        var mode = Enum.Parse<DeveloperSaveMode>(modeText, ignoreCase: true);
        return CommandResult.Request(
            "save_requested",
            $"Requested {mode} save.",
            new DeveloperSaveRequestIntent(mode));
    }
}

public sealed class RenderingFeatureCommand : TypedConsoleCommand
{
    public RenderingFeatureCommand()
        : base(new CommandSpecification(
            "render",
            "Requests a renderer feature toggle without mutating simulation state.",
            new[]
            {
                new CommandArgumentSpecification(
                    "feature",
                    CommandArgumentType.Choice,
                    choices: DeveloperCommandVocabulary.RenderingFeatures,
                    description: "Renderer feature id."),
                new CommandArgumentSpecification(
                    "value",
                    CommandArgumentType.Boolean,
                    false,
                    "Optional on, off, or toggle state.")
            },
            aliases: new[] { "gfx" },
            examples: new[] { "/render lighting on", "/render reflections toggle" },
            category: CommandCategory.Rendering,
            searchTerms: new[] { "graphics", "lighting", "shadow", "ray", "reflection", "post processing" },
            requestIntentType: typeof(SetRenderingFeatureIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var feature = arguments.GetString("feature");
        var toggle = DeveloperCommandParsing.ParseToggle(arguments.GetOptionalString("value"));
        return CommandResult.Request(
            "render_feature_requested",
            $"Requested renderer feature {feature}: {toggle}.",
            new SetRenderingFeatureIntent(feature, toggle));
    }
}

public sealed class LightingDebugCommand : TypedConsoleCommand
{
    public LightingDebugCommand()
        : base(new CommandSpecification(
            "lighting",
            "Requests a lighting diagnostic visualization.",
            new[]
            {
                new CommandArgumentSpecification(
                    "view",
                    CommandArgumentType.Choice,
                    choices: DeveloperCommandVocabulary.LightingDebugViews,
                    description: "Lighting debug view."),
                new CommandArgumentSpecification(
                    "value",
                    CommandArgumentType.Boolean,
                    false,
                    "Optional on, off, or toggle state.")
            },
            aliases: new[] { "lightdebug" },
            examples: new[] { "/lighting rays on", "/lighting timings toggle" },
            category: CommandCategory.Rendering,
            searchTerms: new[] { "lightmap", "occluder", "shadow", "raytracing", "temporal", "reflection" },
            requestIntentType: typeof(SetLightingDebugViewIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var view = arguments.GetString("view");
        var toggle = DeveloperCommandParsing.ParseToggle(arguments.GetOptionalString("value"));
        return CommandResult.Request(
            "lighting_debug_requested",
            $"Requested lighting debug view {view}: {toggle}.",
            new SetLightingDebugViewIntent(view, toggle));
    }
}

public sealed class GameRuleCommand : TypedConsoleCommand
{
    private static IReadOnlyList<string> RuleTargets => DeveloperCommandVocabulary.GameRuleTargets;

    public GameRuleCommand()
        : base(new CommandSpecification(
            "rule",
            "Lists, reads, resets, or requests an authoritative gameplay rule.",
            new[]
            {
                new CommandArgumentSpecification(
                    "ruleId",
                    CommandArgumentType.Identifier,
                    false,
                    "Rule id or list.",
                    choices: RuleTargets),
                new CommandArgumentSpecification(
                    "value",
                    CommandArgumentType.Text,
                    false,
                    "New value, toggle, or reset.",
                    suggestionSource: CommandSuggestionSource.GameRuleValues)
            },
            aliases: new[] { "gamerule" },
            examples: new[] { "/rule", "/rule list", "/rule enemy-spawning off", "/rule mining-speed 2" },
            category: CommandCategory.World,
            searchTerms: new[] { "gameplay", "rules", "toggle", "difficulty" },
            requestIntentType: typeof(GameRuleRequestIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var ruleId = arguments.GetOptionalString("ruleId") ?? "list";
        if (ruleId.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Has("value"))
            {
                return CommandResult.Failure("too_many_arguments", "/rule list does not accept a value.");
            }

            return CommandResult.Success(
                "game_rule_list",
                $"Rules: {string.Join(", ", DeveloperCommandVocabulary.GameRules)}.");
        }

        if (!Contains(DeveloperCommandVocabulary.GameRules, ruleId))
        {
            return CommandResult.Failure("unknown_game_rule", $"Unknown game rule '{ruleId}'.");
        }

        var value = arguments.GetOptionalString("value");
        if (ValidateRuleValue(ruleId, value) is { } validationFailure)
        {
            return validationFailure;
        }

        var kind = value is null
            ? GameRuleRequestKind.Read
            : value.Equals("reset", StringComparison.OrdinalIgnoreCase)
                ? GameRuleRequestKind.Reset
                : GameRuleRequestKind.Set;
        return CommandResult.Request(
            "game_rule_requested",
            $"Requested game rule {ruleId}{(value is null ? string.Empty : $" = {value}")}.",
            new GameRuleRequestIntent(kind, ruleId, kind == GameRuleRequestKind.Set ? value : null));
    }

    private static CommandResult? ValidateRuleValue(string ruleId, string? value)
    {
        if (value is null || value.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedRuleId = ruleId.ToLowerInvariant();
        if (normalizedRuleId is "weather" or "friendly-fire")
        {
            return IsBooleanToggle(value)
                ? null
                : CommandResult.Failure(
                    "invalid_game_rule_value",
                    $"{ruleId} must be on, off, true, false, 1, 0, toggle, or reset.");
        }

        var range = normalizedRuleId switch
        {
            "enemy-spawning" => (Minimum: 0f, Maximum: 10f),
            "time-flow" => (Minimum: 0f, Maximum: 20f),
            "mana-cost" => (Minimum: 0f, Maximum: 10f),
            "building-range" => (Minimum: 0.25f, Maximum: 5f),
            "mining-speed" => (Minimum: 0.05f, Maximum: 20f),
            _ => (Minimum: float.NaN, Maximum: float.NaN)
        };
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier) &&
            float.IsFinite(multiplier) &&
            multiplier >= range.Minimum &&
            multiplier <= range.Maximum)
        {
            return null;
        }

        if (IsBoolean(value))
        {
            return null;
        }

        return CommandResult.Failure(
            "invalid_game_rule_value",
            $"{ruleId} must be {range.Minimum:0.##}-{range.Maximum:0.##}, on/off, or reset.");
    }

    private static bool IsBooleanToggle(string value)
    {
        return value.Equals("toggle", StringComparison.OrdinalIgnoreCase) || IsBoolean(value);
    }

    private static bool IsBoolean(string value)
    {
        return value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("0", StringComparison.Ordinal);
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class PlayerResourceCommand : TypedConsoleCommand
{
    private readonly PlayerResourceKind _resource;

    public PlayerResourceCommand(string name, PlayerResourceKind resource)
        : base(new CommandSpecification(
            name,
            $"Reads or requests a player {name} change.",
            new[]
            {
                new CommandArgumentSpecification(
                    "action",
                    CommandArgumentType.Choice,
                    false,
                    "Resource operation.",
                    DeveloperCommandVocabulary.PlayerResourceActions),
                new CommandArgumentSpecification(
                    "amount",
                    CommandArgumentType.Number,
                    false,
                    "Finite non-negative amount.",
                    minimum: 0)
            },
            examples: new[] { $"/{name}", $"/{name} fill", $"/{name} set 100" },
            category: CommandCategory.Player,
            searchTerms: new[] { "player", "resource", "combat", "magic", "vitals" },
            requestIntentType: typeof(PlayerResourceRequestIntent)))
    {
        _resource = resource;
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var actionText = arguments.GetOptionalString("action") ?? "status";
        var operation = Enum.Parse<PlayerResourceOperation>(actionText, ignoreCase: true);
        var hasAmount = arguments.Has("amount");
        if (operation is PlayerResourceOperation.Set or PlayerResourceOperation.Add && !hasAmount)
        {
            return CommandResult.Failure(
                "missing_argument",
                $"/{Name} {actionText} requires an amount.");
        }

        if (operation is PlayerResourceOperation.Status or PlayerResourceOperation.Fill && hasAmount)
        {
            return CommandResult.Failure(
                "too_many_arguments",
                $"/{Name} {actionText} does not accept an amount.");
        }

        float? amount = hasAmount ? arguments.GetSingle("amount") : null;
        return CommandResult.Request(
            "player_resource_requested",
            $"Requested {_resource} operation {operation}{(amount is null ? string.Empty : $" {amount:0.##}")}.",
            new PlayerResourceRequestIntent(_resource, operation, amount));
    }
}

public sealed class SpawnProjectileCommand : TypedConsoleCommand
{
    public SpawnProjectileCommand()
        : base(new CommandSpecification(
            "projectile",
            "Requests an authoritative projectile spawn.",
            new[]
            {
                new CommandArgumentSpecification(
                    "projectileId",
                    CommandArgumentType.Identifier,
                    description: "Registered projectile id.",
                    suggestionSource: CommandSuggestionSource.Projectiles),
                new CommandArgumentSpecification("x", CommandArgumentType.Number, false, "World X position."),
                new CommandArgumentSpecification("y", CommandArgumentType.Number, false, "World Y position."),
                new CommandArgumentSpecification("directionX", CommandArgumentType.Number, false, "Direction X."),
                new CommandArgumentSpecification("directionY", CommandArgumentType.Number, false, "Direction Y.")
            },
            aliases: new[] { "proj" },
            examples: new[] { "/projectile wooden_arrow", "/projectile magic_bolt 20 40 1 0" },
            category: CommandCategory.Combat,
            searchTerms: new[] { "spawn", "magic", "wand", "arrow", "collision" },
            requestIntentType: typeof(SpawnProjectileRequestIntent)))
    {
    }

    protected override CommandResult Execute(CommandContext context, CommandArguments arguments)
    {
        var projectileId = arguments.GetString("projectileId");
        if (context.Content is not null && !context.Content.Projectiles.TryGetById(projectileId, out _))
        {
            return CommandResult.Failure("unknown_projectile", $"Unknown projectile '{projectileId}'.");
        }

        if (arguments.Has("x") != arguments.Has("y"))
        {
            return CommandResult.Failure("invalid_position", "Projectile X and Y must be provided together.");
        }

        if (arguments.Has("directionX") != arguments.Has("directionY"))
        {
            return CommandResult.Failure("invalid_direction", "Projectile direction X and Y must be provided together.");
        }

        var position = arguments.Has("x")
            ? new Vector2(arguments.GetSingle("x"), arguments.GetSingle("y"))
            : context.PlayerPosition ?? Vector2.Zero;
        var direction = arguments.Has("directionX")
            ? new Vector2(arguments.GetSingle("directionX"), arguments.GetSingle("directionY"))
            : Vector2.UnitX;
        if (direction.LengthSquared() <= 0.000001f || !float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            return CommandResult.Failure("invalid_direction", "Projectile direction must be finite and non-zero.");
        }

        direction = Vector2.Normalize(direction);
        return CommandResult.Request(
            "projectile_spawn_requested",
            $"Requested projectile {projectileId} at {position.X:0.##}, {position.Y:0.##}.",
            new SpawnProjectileRequestIntent(projectileId, position, direction));
    }
}
