using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Equipment;
using Xunit;

namespace Game.Tests.EffectTests;

public sealed class StatusEffectCollectionTests
{
    [Fact]
    public void Update_AppliesDamageTicksAndExpiresEffect()
    {
        var effects = new StatusEffectCollection();
        var health = new HealthComponent(maxHealth: 20);
        effects.Apply(new StatusEffectDefinition
        {
            Id = "poisoned",
            DisplayName = "Poisoned",
            Kind = StatusEffectKind.Debuff,
            DurationSeconds = 2f,
            TickIntervalSeconds = 1f,
            DamagePerTick = 3
        });

        var first = effects.Update(1f, health);
        var second = effects.Update(1f, health);

        Assert.Equal(3, first.DamageApplied);
        Assert.Equal(3, second.DamageApplied);
        Assert.Equal(14, health.Current);
        Assert.False(effects.HasEffect("poisoned"));
        Assert.Equal(1, second.ExpiredCount);
    }

    [Fact]
    public void Apply_RefreshesExistingEffectDuration()
    {
        var definition = new StatusEffectDefinition
        {
            Id = "regeneration",
            DisplayName = "Regeneration",
            Kind = StatusEffectKind.Buff,
            DurationSeconds = 3f,
            TickIntervalSeconds = 1f,
            HealPerTick = 1
        };
        var effects = new StatusEffectCollection();
        effects.Apply(definition);
        effects.Update(1f);

        effects.Apply(definition);

        var active = Assert.Single(effects.ActiveEffects);
        Assert.Equal(3f, active.RemainingSeconds, precision: 3);
    }

    [Fact]
    public void TryApply_ReportsOnlyNewOrExtendedEffectsAsChanges()
    {
        var definition = new StatusEffectDefinition
        {
            Id = "fortified",
            DisplayName = "Fortified",
            Kind = StatusEffectKind.Buff,
            DurationSeconds = 4f,
            DefenseDelta = 2
        };
        var effects = new StatusEffectCollection();

        var added = effects.TryApply(definition, durationSeconds: null, out var initiallyRefreshed);
        var unchanged = effects.TryApply(definition, durationSeconds: 3f, out _);
        effects.Update(2f);
        var refreshed = effects.TryApply(definition, durationSeconds: 4f, out var wasRefreshed);

        Assert.True(added);
        Assert.False(initiallyRefreshed);
        Assert.False(unchanged);
        Assert.True(refreshed);
        Assert.True(wasRefreshed);
        Assert.Equal(4f, Assert.Single(effects.ActiveEffects).RemainingSeconds, precision: 3);
    }

    [Fact]
    public void ApplyDetailed_ReturnsEffectIdentityAndSkipsNoOpRefresh()
    {
        var definition = new StatusEffectDefinition
        {
            Id = "swift",
            DisplayName = "Swift",
            Kind = StatusEffectKind.Buff,
            DurationSeconds = 5f,
            MovementSpeedBonus = 0.1f
        };
        var registry = StatusEffectRegistry.Create(new[] { definition });
        var effects = new StatusEffectCollection();
        var applier = new StatusEffectApplier(new Random(1));
        var applications = new[] { new StatusEffectApplication { EffectId = "swift" } };

        var first = applier.ApplyDetailed(effects, registry, applications);
        var repeated = applier.ApplyDetailed(effects, registry, applications);

        var applied = Assert.Single(first.AppliedEffects);
        Assert.Equal("swift", applied.EffectId);
        Assert.False(applied.Refreshed);
        Assert.True(repeated == StatusEffectApplyResult.None);
        Assert.False(repeated.Changed);
    }

    [Fact]
    public void ApplyStatModifiers_CombinesActiveEffectBonuses()
    {
        var effects = new StatusEffectCollection();
        effects.Apply(new StatusEffectDefinition
        {
            Id = "well_fed",
            DisplayName = "Well Fed",
            Kind = StatusEffectKind.Buff,
            DurationSeconds = 10f,
            DefenseDelta = 2,
            MovementSpeedBonus = 0.05f,
            MeleeDamageBonus = 0.10f
        });

        var stats = effects.ApplyStatModifiers(PlayerStatBlock.Base);

        Assert.Equal(2, stats.Defense);
        Assert.Equal(1.05f, stats.MovementSpeedMultiplier, precision: 3);
        Assert.Equal(1.10f, stats.MeleeDamageMultiplier, precision: 3);
    }

    [Fact]
    public void JsonLoader_LoadsStatusEffectDefinition()
    {
        var definition = new StatusEffectDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "poisoned",
          "displayName": "Poisoned",
          "kind": "Debuff",
          "durationSeconds": 6.0,
          "tickIntervalSeconds": 1.0,
          "damagePerTick": 2
        }
        """);

        Assert.Equal("poisoned", definition.Id);
        Assert.Equal(StatusEffectKind.Debuff, definition.Kind);
        Assert.Equal(2, definition.DamagePerTick);
    }
}
