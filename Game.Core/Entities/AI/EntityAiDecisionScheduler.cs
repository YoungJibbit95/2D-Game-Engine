using System.Numerics;

namespace Game.Core.Entities.AI;

public sealed record EntityAiSchedulingOptions
{
    public int DecisionBudgetPerTick { get; init; } = 256;

    public int FullRatePopulationThreshold { get; init; } = 256;

    public float NearDistance { get; init; } = 640f;

    public float MidDistance { get; init; } = 2_048f;

    public int MidCadenceTicks { get; init; } = 2;

    public int FarCadenceTicks { get; init; } = 8;

    public int StarvationThresholdTicks { get; init; } = 16;

    internal EntityAiSchedulingOptions Validate(int maximumActors)
    {
        if (DecisionBudgetPerTick <= 0 || DecisionBudgetPerTick > maximumActors)
        {
            throw new ArgumentOutOfRangeException(nameof(DecisionBudgetPerTick));
        }

        if (FullRatePopulationThreshold < 0 || FullRatePopulationThreshold > maximumActors)
        {
            throw new ArgumentOutOfRangeException(nameof(FullRatePopulationThreshold));
        }

        if (!float.IsFinite(NearDistance) ||
            !float.IsFinite(MidDistance) ||
            NearDistance < 0 ||
            MidDistance < NearDistance)
        {
            throw new ArgumentOutOfRangeException(nameof(NearDistance));
        }

        if (MidCadenceTicks <= 0 || FarCadenceTicks < MidCadenceTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(MidCadenceTicks));
        }

        if (StarvationThresholdTicks < FarCadenceTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(StarvationThresholdTicks));
        }

        return this;
    }
}

public readonly record struct EntityAiSchedulingTelemetry(
    long TickNumber,
    int ActiveActors,
    int DecisionBudget,
    int DecisionsScheduled,
    int FullRateDecisions,
    int NearDecisions,
    int EngagedDecisions,
    int MidDecisions,
    int FarDecisions,
    int EligibleBudgetedDecisions,
    int DecisionsDeferred,
    int StarvationPromotions,
    int StarvationThresholdTicks,
    int OldestEligibleDecisionAgeTicks,
    int PhysicsBodiesSubmitted)
{
    public int MandatoryDecisions => FullRateDecisions + NearDecisions + EngagedDecisions;

    public int BudgetOverrun => Math.Max(0, DecisionsScheduled - DecisionBudget);

    public bool Overloaded => BudgetOverrun > 0 || DecisionsDeferred > 0;

    public bool FairnessPressure => StarvationPromotions > 0 ||
                                    OldestEligibleDecisionAgeTicks >= StarvationThresholdTicks;
}

internal enum EntityAiDecisionTier : byte
{
    Mid,
    Far
}

internal readonly record struct EntityAiSchedule(
    long Epoch,
    long DecisionStep,
    EntityAiSchedulingTelemetry Telemetry);

internal sealed class EntityAiDecisionScheduler
{
    private readonly Candidate[] _selected;
    private readonly EntityAiSchedulingOptions _options;
    private long _epoch;
    private long _decisionStep = -1;

    public EntityAiDecisionScheduler(int maximumActors, EntityAiSchedulingOptions? options = null)
    {
        if (maximumActors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumActors));
        }

        _options = (options ?? new EntityAiSchedulingOptions
        {
            DecisionBudgetPerTick = Math.Min(256, maximumActors),
            FullRatePopulationThreshold = Math.Min(256, maximumActors)
        }).Validate(maximumActors);
        _selected = new Candidate[_options.DecisionBudgetPerTick];
    }

    public EntityAiSchedulingOptions Options => _options;

    public EntityAiSchedule Schedule(
        IReadOnlyList<Entity> entities,
        PlayerEntity? player,
        long tickNumber)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _epoch = _epoch == long.MaxValue ? 1 : _epoch + 1;
        _decisionStep++;
        var epoch = _epoch;
        var activeActors = CountActiveActors(entities);
        var preserveFullRate = player is not { IsActive: true } ||
                               activeActors <= _options.FullRatePopulationThreshold;
        var playerCenter = player?.Body.Center ?? default;
        var nearDistanceSquared = _options.NearDistance * _options.NearDistance;
        var midDistanceSquared = _options.MidDistance * _options.MidDistance;
        var selectedCount = 0;
        var fullRateDecisions = 0;
        var nearDecisions = 0;
        var engagedDecisions = 0;
        var midDecisions = 0;
        var farDecisions = 0;
        var eligibleBudgetedDecisions = 0;
        var oldestEligibleAge = 0;

        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index] is not EnemyEntity { IsActive: true, Health.IsDead: false } actor)
            {
                continue;
            }

            var engaged = IsEngaged(actor);
            var distanceSquared = player is { IsActive: true }
                ? Vector2.DistanceSquared(actor.Body.Center, playerCenter)
                : 0f;
            if (engaged)
            {
                actor.ScheduleAiDecision(epoch);
                engagedDecisions++;
                continue;
            }

            if (preserveFullRate)
            {
                actor.ScheduleAiDecision(epoch);
                fullRateDecisions++;
                continue;
            }

            if (distanceSquared <= nearDistanceSquared)
            {
                actor.ScheduleAiDecision(epoch);
                nearDecisions++;
                continue;
            }

            var tier = distanceSquared <= midDistanceSquared
                ? EntityAiDecisionTier.Mid
                : EntityAiDecisionTier.Far;
            var rawAge = actor.GetAiDecisionAge(_decisionStep);
            var age = rawAge;
            var cadence = tier == EntityAiDecisionTier.Mid
                ? _options.MidCadenceTicks
                : _options.FarCadenceTicks;
            if (age < cadence)
            {
                continue;
            }

            eligibleBudgetedDecisions++;
            oldestEligibleAge = Math.Max(
                oldestEligibleAge,
                age == int.MaxValue ? _options.StarvationThresholdTicks : age);
            var candidate = new Candidate(
                actor,
                tier,
                age,
                age >= _options.StarvationThresholdTicks,
                BuildTiePriority(tickNumber, actor.Id));
            Offer(candidate, ref selectedCount);
        }

        var remainingBudget = Math.Max(
            0,
            _options.DecisionBudgetPerTick - fullRateDecisions - nearDecisions - engagedDecisions);
        var scheduledBudgeted = Math.Min(selectedCount, remainingBudget);
        if (scheduledBudgeted < selectedCount)
        {
            TrimToBudget(remainingBudget, ref selectedCount);
        }

        var starvationPromotions = 0;
        for (var index = 0; index < selectedCount; index++)
        {
            ref readonly var candidate = ref _selected[index];
            candidate.Actor.ScheduleAiDecision(epoch);
            if (candidate.Tier == EntityAiDecisionTier.Mid)
            {
                midDecisions++;
            }
            else
            {
                farDecisions++;
            }

            if (candidate.Starved)
            {
                starvationPromotions++;
            }
        }

        Array.Clear(_selected, 0, selectedCount);
        var decisionsScheduled = fullRateDecisions + nearDecisions + engagedDecisions + selectedCount;
        var telemetry = new EntityAiSchedulingTelemetry(
            tickNumber,
            activeActors,
            _options.DecisionBudgetPerTick,
            decisionsScheduled,
            fullRateDecisions,
            nearDecisions,
            engagedDecisions,
            midDecisions,
            farDecisions,
            eligibleBudgetedDecisions,
            Math.Max(0, eligibleBudgetedDecisions - selectedCount),
            starvationPromotions,
            _options.StarvationThresholdTicks,
            oldestEligibleAge,
            0);
        return new EntityAiSchedule(epoch, _decisionStep, telemetry);
    }

    private int CountActiveActors(IReadOnlyList<Entity> entities)
    {
        var count = 0;
        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index] is EnemyEntity { IsActive: true, Health.IsDead: false })
            {
                count++;
            }
        }

        return count;
    }

    private void Offer(Candidate candidate, ref int count)
    {
        if (count < _selected.Length)
        {
            _selected[count] = candidate;
            SiftWorstUp(count);
            count++;
            return;
        }

        if (_selected.Length == 0 || !HasHigherPriority(candidate, _selected[0]))
        {
            return;
        }

        _selected[0] = candidate;
        SiftWorstDown(0, count);
    }

    private void TrimToBudget(int budget, ref int count)
    {
        while (count > budget)
        {
            count--;
            _selected[0] = _selected[count];
            _selected[count] = default;
            SiftWorstDown(0, count);
        }
    }

    private void SiftWorstUp(int child)
    {
        while (child > 0)
        {
            var parent = (child - 1) / 2;
            if (!IsWorse(_selected[child], _selected[parent]))
            {
                return;
            }

            (_selected[parent], _selected[child]) = (_selected[child], _selected[parent]);
            child = parent;
        }
    }

    private void SiftWorstDown(int root, int count)
    {
        while (true)
        {
            var left = root * 2 + 1;
            if (left >= count)
            {
                return;
            }

            var worst = left;
            var right = left + 1;
            if (right < count && IsWorse(_selected[right], _selected[left]))
            {
                worst = right;
            }

            if (!IsWorse(_selected[worst], _selected[root]))
            {
                return;
            }

            (_selected[root], _selected[worst]) = (_selected[worst], _selected[root]);
            root = worst;
        }
    }

    private static bool IsWorse(in Candidate left, in Candidate right)
    {
        return HasHigherPriority(right, left);
    }

    private static bool HasHigherPriority(in Candidate left, in Candidate right)
    {
        if (left.Starved != right.Starved)
        {
            return left.Starved;
        }

        if (left.Age != right.Age)
        {
            return left.Age > right.Age;
        }

        if (left.Tier != right.Tier)
        {
            return left.Tier < right.Tier;
        }

        if (left.TiePriority != right.TiePriority)
        {
            return left.TiePriority < right.TiePriority;
        }

        return left.Actor.Id < right.Actor.Id;
    }

    private static bool IsEngaged(EnemyEntity actor)
    {
        return actor.TargetEntityId is not null ||
               actor.AiState is AiState.Attack or AiState.Chase or AiState.Flee or AiState.Investigate;
    }

    private static uint BuildTiePriority(long tickNumber, int entityId)
    {
        var value = unchecked((ulong)tickNumber) ^ (unchecked((ulong)(uint)entityId) * 0x9E3779B97F4A7C15UL);
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return unchecked((uint)value);
    }

    private readonly record struct Candidate(
        EnemyEntity Actor,
        EntityAiDecisionTier Tier,
        int Age,
        bool Starved,
        uint TiePriority);
}
