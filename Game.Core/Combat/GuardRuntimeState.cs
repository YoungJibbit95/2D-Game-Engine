using System.Numerics;

namespace Game.Core.Combat;

public sealed class GuardRuntimeState
{
    public GuardRuntimeState(GuardDefinition definition, float? stamina = null, float guardBreakRemainingSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (stamina is { } explicitStamina && (!float.IsFinite(explicitStamina) || explicitStamina < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(stamina));
        }

        if (!float.IsFinite(guardBreakRemainingSeconds) || guardBreakRemainingSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guardBreakRemainingSeconds));
        }

        Definition = definition;
        Stamina = Math.Clamp(stamina ?? definition.MaxStamina, 0, definition.MaxStamina);
        GuardBreakRemainingSeconds = guardBreakRemainingSeconds;
        Facing = Vector2.UnitX;
    }

    public GuardDefinition Definition { get; }

    public float Stamina { get; private set; }

    public Vector2 Facing { get; private set; }

    public bool IsGuarding { get; private set; }

    public float GuardElapsedSeconds { get; private set; }

    public float GuardBreakRemainingSeconds { get; private set; }

    public bool IsGuardBroken => GuardBreakRemainingSeconds > 0;

    public bool IsBlockWindowOpen =>
        IsGuarding &&
        !IsGuardBroken &&
        GuardElapsedSeconds >= Definition.BlockWindowStartSeconds &&
        (Definition.BlockWindowEndSeconds is null ||
         GuardElapsedSeconds <= Definition.BlockWindowEndSeconds);

    public bool IsParryWindowOpen =>
        IsBlockWindowOpen &&
        GuardElapsedSeconds >= Definition.ParryWindowStartSeconds &&
        GuardElapsedSeconds <= Definition.ParryWindowStartSeconds + Definition.ParryWindowSeconds;

    public bool TryBeginGuard(Vector2 facing)
    {
        if (IsGuardBroken || Stamina <= 0 || !TryNormalize(facing, out var normalized))
        {
            return false;
        }

        Facing = normalized;
        if (IsGuarding)
        {
            return true;
        }

        GuardElapsedSeconds = 0;
        IsGuarding = true;
        return true;
    }

    public GuardCommandResult ApplyCommand(GuardCommand command)
    {
        var accepted = command.Kind switch
        {
            GuardCommandKind.Begin => TryBeginGuard(command.Facing),
            GuardCommandKind.End => ApplyEndCommand(),
            GuardCommandKind.Toggle => ApplyToggleCommand(command.Facing),
            GuardCommandKind.UpdateFacing => TrySetFacing(command.Facing),
            _ => false
        };

        return new GuardCommandResult(accepted, IsGuarding, IsGuardBroken, Stamina);
    }

    public bool TrySetFacing(Vector2 facing)
    {
        if (!TryNormalize(facing, out var normalized))
        {
            return false;
        }

        Facing = normalized;
        return true;
    }

    public void EndGuard()
    {
        IsGuarding = false;
        GuardElapsedSeconds = 0;
    }

    public void Update(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var remaining = deltaSeconds;
        if (GuardBreakRemainingSeconds > 0)
        {
            var breakTime = Math.Min(remaining, GuardBreakRemainingSeconds);
            GuardBreakRemainingSeconds -= breakTime;
            remaining -= breakTime;
            if (GuardBreakRemainingSeconds > 0)
            {
                return;
            }
        }

        if (IsGuarding)
        {
            GuardElapsedSeconds += remaining;
            return;
        }

        Stamina = Math.Min(
            Definition.MaxStamina,
            Stamina + Definition.StaminaRegenerationPerSecond * remaining);
    }

    public bool CoversImpact(Vector2 impactDirection)
    {
        if (!IsBlockWindowOpen || !TryNormalize(impactDirection, out var normalizedImpact))
        {
            return false;
        }

        // Impact direction points from attacker to defender, so the source lies in the opposite direction.
        var directionToSource = -normalizedImpact;
        var minimumDot = MathF.Cos(Definition.ShieldArcRadians * 0.5f);
        return Vector2.Dot(Facing, directionToSource) >= minimumDot;
    }

    internal float SpendStamina(float amount)
    {
        var spent = Math.Min(Stamina, Math.Max(0, amount));
        Stamina -= spent;
        return spent;
    }

    internal void Break()
    {
        Stamina = 0;
        IsGuarding = false;
        GuardElapsedSeconds = 0;
        GuardBreakRemainingSeconds = Definition.GuardBreakDurationSeconds;
    }

    private static bool TryNormalize(Vector2 value, out Vector2 normalized)
    {
        if (!float.IsFinite(value.X) ||
            !float.IsFinite(value.Y) ||
            value.LengthSquared() <= float.Epsilon)
        {
            normalized = Vector2.Zero;
            return false;
        }

        normalized = Vector2.Normalize(value);
        return true;
    }

    private bool ApplyEndCommand()
    {
        EndGuard();
        return true;
    }

    private bool ApplyToggleCommand(Vector2 facing)
    {
        if (IsGuarding)
        {
            EndGuard();
            return true;
        }

        return TryBeginGuard(facing);
    }
}
