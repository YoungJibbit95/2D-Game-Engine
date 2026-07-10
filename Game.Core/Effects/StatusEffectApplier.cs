namespace Game.Core.Effects;

public sealed class StatusEffectApplier
{
    private readonly Random _random;

    public StatusEffectApplier(Random? random = null)
    {
        _random = random ?? new Random();
    }

    public int Apply(
        StatusEffectCollection target,
        StatusEffectRegistry registry,
        IEnumerable<StatusEffectApplication> applications)
    {
        return ApplyDetailed(target, registry, applications).AppliedCount;
    }

    public StatusEffectApplyResult ApplyDetailed(
        StatusEffectCollection target,
        StatusEffectRegistry registry,
        IEnumerable<StatusEffectApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(applications);

        var applied = new List<AppliedStatusEffectResult>();
        foreach (var application in applications)
        {
            if (string.IsNullOrWhiteSpace(application.EffectId) ||
                !registry.TryGetById(application.EffectId, out var definition) ||
                !Roll(application.Chance))
            {
                continue;
            }

            var duration = application.DurationSeconds ?? definition.DurationSeconds;
            if (target.TryApply(definition, duration, out var refreshed))
            {
                applied.Add(new AppliedStatusEffectResult(definition.Id, refreshed, duration));
            }
        }

        return applied.Count == 0
            ? StatusEffectApplyResult.None
            : new StatusEffectApplyResult(applied);
    }

    private bool Roll(float chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        return chance >= 1f || _random.NextDouble() <= chance;
    }
}
