using Game.Core.Effects;
using Game.Core.Equipment;
using Game.Core.Items;
using Game.Core.Projectiles;

namespace Game.Core.Combat;

public static class MagicAttackResolver
{
    public static ProjectileDefinition ResolveProjectile(
        ProjectileDefinition projectile,
        ItemDefinition sourceItem,
        PlayerStatBlock stats)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(sourceItem);

        var damageMultiplier = float.IsFinite(stats.MagicDamageMultiplier)
            ? Math.Max(0f, stats.MagicDamageMultiplier)
            : 0f;
        var baseDamage = Math.Max(0L, (long)projectile.Damage + sourceItem.Damage);
        var scaledDamage = baseDamage * damageMultiplier;
        var damage = scaledDamage >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)MathF.Round((float)scaledDamage));

        return projectile with
        {
            Damage = damage,
            DamageType = DamageType.Magic,
            Knockback = ResolveKnockback(projectile.Knockback, sourceItem.Knockback),
            OnHitEffects = MergeStatusEffects(projectile.OnHitEffects, sourceItem.OnHitEffects)
        };
    }

    private static float ResolveKnockback(float projectileMultiplier, float itemKnockback)
    {
        var normalizedProjectile = float.IsFinite(projectileMultiplier)
            ? Math.Max(0f, projectileMultiplier)
            : 0f;
        var normalizedItem = float.IsFinite(itemKnockback)
            ? Math.Max(0f, itemKnockback)
            : 0f;
        return normalizedItem > 0f
            ? normalizedItem * normalizedProjectile
            : normalizedProjectile;
    }

    private static IReadOnlyList<StatusEffectApplication> MergeStatusEffects(
        IReadOnlyList<StatusEffectApplication> projectileEffects,
        IReadOnlyList<StatusEffectApplication> itemEffects)
    {
        ArgumentNullException.ThrowIfNull(projectileEffects);
        ArgumentNullException.ThrowIfNull(itemEffects);
        if (itemEffects.Count == 0)
        {
            return projectileEffects;
        }

        if (projectileEffects.Count == 0)
        {
            return CopyEffects(itemEffects);
        }

        var merged = new List<StatusEffectApplication>(projectileEffects.Count + itemEffects.Count);
        for (var index = 0; index < projectileEffects.Count; index++)
        {
            merged.Add(projectileEffects[index]);
        }

        for (var itemIndex = 0; itemIndex < itemEffects.Count; itemIndex++)
        {
            var incoming = itemEffects[itemIndex];
            var existingIndex = FindEffect(merged, incoming.EffectId);
            if (existingIndex < 0)
            {
                merged.Add(incoming);
                continue;
            }

            var existing = merged[existingIndex];
            var existingChance = Math.Clamp(existing.Chance, 0f, 1f);
            var incomingChance = Math.Clamp(incoming.Chance, 0f, 1f);
            merged[existingIndex] = existing with
            {
                Chance = 1f - ((1f - existingChance) * (1f - incomingChance)),
                DurationSeconds = ResolveDuration(existing.DurationSeconds, incoming.DurationSeconds)
            };
        }

        return merged.ToArray();
    }

    private static StatusEffectApplication[] CopyEffects(IReadOnlyList<StatusEffectApplication> effects)
    {
        var copy = new StatusEffectApplication[effects.Count];
        for (var index = 0; index < effects.Count; index++)
        {
            copy[index] = effects[index];
        }

        return copy;
    }

    private static int FindEffect(List<StatusEffectApplication> effects, string effectId)
    {
        for (var index = 0; index < effects.Count; index++)
        {
            if (string.Equals(effects[index].EffectId, effectId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static float? ResolveDuration(float? existing, float? incoming)
    {
        if (existing is null || incoming is null)
        {
            return null;
        }

        return Math.Max(existing.Value, incoming.Value);
    }
}