namespace Game.Core.Combat;

public static class AttackSequenceCompiler
{
    public const int DefaultTicksPerSecond = 60;

    public static AttackSequenceDefinition Compile(
        string sequenceId,
        IReadOnlyList<AttackDefinition> attacks,
        int ticksPerSecond = DefaultTicksPerSecond,
        int inputBufferTicks = 6,
        int commandCapacity = 16,
        int eventCapacity = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceId);
        ArgumentNullException.ThrowIfNull(attacks);
        if (attacks.Count == 0)
        {
            throw new ArgumentException("At least one attack is required.", nameof(attacks));
        }

        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        }

        var steps = new AttackComboStepDefinition[attacks.Count];
        for (var index = 0; index < attacks.Count; index++)
        {
            var attack = attacks[index] ?? throw new ArgumentException("Attacks cannot contain null entries.", nameof(attacks));
            attack.Validate();
            var startupTicks = ToOptionalTicks(attack.WindupSeconds, ticksPerSecond);
            var activeTicks = ToRequiredTicks(attack.ActiveSeconds, ticksPerSecond);
            var recoveryTicks = ToOptionalTicks(attack.RecoverySeconds, ticksPerSecond);
            var cooldownTicks = ToOptionalTicks(attack.CooldownSeconds, ticksPerSecond);
            AttackPhaseWindow? comboWindow = null;
            if (index < attacks.Count - 1)
            {
                if (attack.ComboWindowStartSeconds is not { } start || attack.ComboWindowEndSeconds is not { } end)
                {
                    throw new InvalidOperationException("Every non-terminal legacy attack requires a combo window.");
                }

                var comboStart = ToOptionalTicks(start, ticksPerSecond);
                var comboEnd = ToRequiredTicks(end, ticksPerSecond);
                comboWindow = new AttackPhaseWindow(comboStart, comboEnd);
            }
            else if (attack.ComboWindowStartSeconds is not null)
            {
                throw new InvalidOperationException("The terminal legacy attack cannot define a combo window.");
            }

            var shapes = attack.MeleeSweep is null
                ? Array.Empty<SweptMeleeShapeDefinition>()
                : new[]
                {
                    new SweptMeleeShapeDefinition
                    {
                        Id = $"{attack.Id}:sweep",
                        Sweep = attack.MeleeSweep,
                        ActiveEndTickExclusive = activeTicks
                    }
                };
            steps[index] = new AttackComboStepDefinition
            {
                Id = attack.Id,
                Timeline = AttackTimelineDefinition.Create(
                    startupTicks,
                    activeTicks,
                    recoveryTicks,
                    cooldownTicks,
                    comboWindow),
                NextStepId = index + 1 < attacks.Count ? attacks[index + 1].Id : null,
                Cost = attack.ResourceCost,
                MaxTargetsPerSwing = attack.MaxTargetsPerSwing,
                MeleeShapes = shapes
            };
        }

        var definition = new AttackSequenceDefinition
        {
            Id = sequenceId,
            Steps = steps,
            InputBufferTicks = inputBufferTicks,
            CommandCapacity = commandCapacity,
            EventCapacity = eventCapacity
        };
        definition.Validate();
        return definition;
    }

    private static int ToOptionalTicks(float seconds, int ticksPerSecond)
    {
        if (seconds <= 0)
        {
            return 0;
        }

        return checked((int)MathF.Round(seconds * ticksPerSecond, MidpointRounding.AwayFromZero));
    }

    private static int ToRequiredTicks(float seconds, int ticksPerSecond) =>
        Math.Max(1, ToOptionalTicks(seconds, ticksPerSecond));
}
