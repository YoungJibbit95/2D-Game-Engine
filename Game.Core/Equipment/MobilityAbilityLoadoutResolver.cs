using Game.Core.Items;
using Game.Core.Movement;

namespace Game.Core.Equipment;

public sealed class MobilityAbilityLoadoutResolver
{
    public MobilityAbilityProfile Resolve(
        EquipmentLoadout loadout,
        IItemDefinitionProvider items,
        SideViewCharacterControllerOptions? movementOptions = null)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(items);
        var options = movementOptions ?? SideViewCharacterControllerOptions.Default;

        var extraJumps = 0;
        var airJumpMultiplier = options.DoubleJumpVelocityMultiplier;
        var canWallJump = false;
        var flightDuration = 0f;
        var flightSpeedMultiplier = 1f;
        var flightAccelerationMultiplier = 1f;
        var glideEnabled = false;
        var glideGravityScale = 1f;
        var glideTerminalVelocity = 0f;

        foreach (var stack in loadout.Slots.Values)
        {
            if (stack.IsEmpty)
            {
                continue;
            }

            var definition = items.GetById(stack.ItemId);
            var profile = ResolveItemProfile(definition, options);
            extraJumps = Math.Min(
                MobilityAbilityDefinition.MaximumExtraJumpCount,
                extraJumps + profile.ExtraJumpCount);
            airJumpMultiplier = Math.Max(airJumpMultiplier, profile.AirJumpVelocityMultiplier);
            canWallJump |= profile.CanWallJump;
            flightDuration = Math.Max(flightDuration, profile.FlightDurationSeconds);
            flightSpeedMultiplier = Math.Max(
                flightSpeedMultiplier,
                profile.FlightVerticalSpeedMultiplier);
            flightAccelerationMultiplier = Math.Max(
                flightAccelerationMultiplier,
                profile.FlightAccelerationMultiplier);

            if (!profile.GlideEnabled)
            {
                continue;
            }

            glideGravityScale = glideEnabled
                ? Math.Min(glideGravityScale, profile.GlideGravityScale)
                : profile.GlideGravityScale;
            glideTerminalVelocity = glideEnabled
                ? Math.Min(glideTerminalVelocity, profile.GlideTerminalVelocity)
                : profile.GlideTerminalVelocity;
            glideEnabled = true;
        }

        var resolved = new MobilityAbilityProfile(
            extraJumps,
            airJumpMultiplier,
            canWallJump,
            flightDuration,
            flightSpeedMultiplier,
            flightAccelerationMultiplier,
            glideEnabled,
            glideEnabled ? glideGravityScale : 1f,
            glideEnabled ? glideTerminalVelocity : 0f);
        resolved.Validate();
        return resolved;
    }

    private static MobilityAbilityProfile ResolveItemProfile(
        ItemDefinition item,
        SideViewCharacterControllerOptions options)
    {
        var profile = item.Mobility is null
            ? MobilityAbilityProfile.Disabled
            : MobilityAbilityProfile.FromDefinition(item.Mobility);

        return profile with
        {
            ExtraJumpCount = item.CanDoubleJump && profile.ExtraJumpCount == 0
                ? options.MaxAirJumps
                : profile.ExtraJumpCount,
            AirJumpVelocityMultiplier = item.CanDoubleJump && profile.ExtraJumpCount == 0
                ? options.DoubleJumpVelocityMultiplier
                : profile.AirJumpVelocityMultiplier,
            CanWallJump = profile.CanWallJump || item.CanWallJump,
            FlightDurationSeconds = item.CanFly && profile.FlightDurationSeconds <= 0f
                ? options.FlightDurationSeconds
                : profile.FlightDurationSeconds,
            GlideEnabled = profile.GlideEnabled || item.CanGlide,
            GlideGravityScale = item.CanGlide && !profile.GlideEnabled
                ? options.GlideGravityScale
                : profile.GlideGravityScale,
            GlideTerminalVelocity = item.CanGlide && !profile.GlideEnabled
                ? options.GlideTerminalVelocity
                : profile.GlideTerminalVelocity
        };
    }
}