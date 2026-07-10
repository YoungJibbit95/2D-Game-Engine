using Game.Core.Combat;
using Game.Core.Equipment;

namespace Game.Core.Effects;

public sealed class StatusEffectCollection
{
    private readonly Dictionary<string, ActiveStatusEffect> _active = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ActiveStatusEffect> ActiveEffects => _active.Values;

    public bool HasEffect(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _active.ContainsKey(id);
    }

    public void Apply(StatusEffectDefinition definition, float? durationSeconds = null)
    {
        TryApply(definition, durationSeconds, out _);
    }

    public bool TryApply(
        StatusEffectDefinition definition,
        float? durationSeconds,
        out bool refreshed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        refreshed = false;

        var duration = durationSeconds ?? definition.DurationSeconds;
        if (duration <= 0)
        {
            return false;
        }

        if (_active.TryGetValue(definition.Id, out var existing))
        {
            if (duration <= existing.RemainingSeconds)
            {
                return false;
            }

            existing.Refresh(duration);
            refreshed = true;
            return true;
        }

        _active.Add(definition.Id, new ActiveStatusEffect(definition, duration));
        return true;
    }

    public bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _active.Remove(id);
    }

    public void Clear()
    {
        _active.Clear();
    }

    public StatusEffectUpdateResult Update(float deltaSeconds, HealthComponent? health = null)
    {
        if (deltaSeconds <= 0 || _active.Count == 0)
        {
            return StatusEffectUpdateResult.None;
        }

        var damageApplied = 0;
        var healingApplied = 0;
        var expired = 0;

        foreach (var effect in _active.Values.ToArray())
        {
            var activeDelta = Math.Min(deltaSeconds, effect.RemainingSeconds);
            effect.TickAccumulator += activeDelta;
            ApplyTicks(effect, health, ref damageApplied, ref healingApplied);
            effect.Advance(deltaSeconds);

            if (effect.RemainingSeconds > 0)
            {
                continue;
            }

            _active.Remove(effect.Definition.Id);
            expired++;
        }

        return new StatusEffectUpdateResult(damageApplied, healingApplied, expired);
    }

    public PlayerStatBlock ApplyStatModifiers(PlayerStatBlock stats)
    {
        var maxHealth = stats.MaxHealth;
        var defense = stats.Defense;
        var movement = stats.MovementSpeedMultiplier;
        var melee = stats.MeleeDamageMultiplier;
        var ranged = stats.RangedDamageMultiplier;
        var mining = stats.MiningSpeedMultiplier;
        var maxMana = stats.MaxMana;
        var magic = stats.MagicDamageMultiplier;
        var manaCost = stats.ManaCostMultiplier;
        var manaRegen = stats.ManaRegenMultiplier;

        foreach (var effect in _active.Values)
        {
            var definition = effect.Definition;
            maxHealth += definition.MaxHealthBonus;
            defense += definition.DefenseDelta;
            movement += definition.MovementSpeedBonus;
            melee += definition.MeleeDamageBonus;
            ranged += definition.RangedDamageBonus;
            mining += definition.MiningSpeedBonus;
        }

        return new PlayerStatBlock(
            MaxHealth: Math.Max(1, maxHealth),
            Defense: Math.Max(0, defense),
            MovementSpeedMultiplier: Math.Max(0.1f, movement),
            MeleeDamageMultiplier: Math.Max(0.1f, melee),
            RangedDamageMultiplier: Math.Max(0.1f, ranged),
            MiningSpeedMultiplier: Math.Max(0.1f, mining),
            MaxMana: Math.Max(0, maxMana),
            MagicDamageMultiplier: Math.Max(0.1f, magic),
            ManaCostMultiplier: Math.Clamp(manaCost, 0.1f, 3f),
            ManaRegenMultiplier: Math.Max(0f, manaRegen));
    }

    private static void ApplyTicks(
        ActiveStatusEffect effect,
        HealthComponent? health,
        ref int damageApplied,
        ref int healingApplied)
    {
        var interval = effect.Definition.TickIntervalSeconds;
        if (interval <= 0)
        {
            return;
        }

        while (effect.TickAccumulator >= interval)
        {
            effect.TickAccumulator -= interval;

            if (health is null)
            {
                continue;
            }

            if (effect.Definition.DamagePerTick > 0 && health.ApplyRawDamage(effect.Definition.DamagePerTick))
            {
                damageApplied += effect.Definition.DamagePerTick;
            }

            if (effect.Definition.HealPerTick > 0)
            {
                var before = health.Current;
                health.Heal(effect.Definition.HealPerTick);
                healingApplied += Math.Max(0, health.Current - before);
            }
        }
    }
}
