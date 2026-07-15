using Game.Client.Rendering.Character;
using Game.Core.Characters;
using Game.Core.Combat;
using Game.Core.Equipment;
using Game.Core.Inventory;
using Game.Core.Runtime;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class Wave04PlayerCharacterRendererTests
{
    [Fact]
    public void Advance_UsesFixedTickRigForLocomotionGuardAndActions()
    {
        var renderer = new Wave04PlayerCharacterRenderer();
        var equipment = new EquipmentLoadout();
        var appearance = new CharacterAppearance();
        var guard = new GuardRuntimeState(new GuardDefinition());

        renderer.Advance(CreateSnapshot(), guard, appearance, equipment);

        Assert.Equal(1, renderer.FixedTick);
        Assert.Equal("idle", renderer.CurrentStateId);
        Assert.Equal(4, renderer.DrawCommandCount);

        Assert.True(guard.TryBeginGuard(Vector2.UnitX));
        renderer.Advance(
            CreateSnapshot(isGuarding: true),
            guard,
            appearance,
            equipment);

        Assert.Equal("block", renderer.CurrentStateId);

        guard.EndGuard();
        renderer.RequestAction(CharacterAnimationState.Mine);
        renderer.Advance(CreateSnapshot(), guard, appearance, equipment);

        Assert.Equal("tool", renderer.CurrentStateId);
        Assert.Equal(3, renderer.FixedTick);
    }

    private static PlayerFrameSnapshot CreateSnapshot(bool isGuarding = false)
    {
        return new PlayerFrameSnapshot(
            Vector2.Zero,
            Vector2.Zero,
            new RectI(20, 30, 12, 28),
            IsOnGround: true,
            IsDead: false,
            Health: 100,
            MaxHealth: 100,
            Mana: 20,
            MaxMana: 20,
            PlayerStatBlock.Base,
            isGuarding,
            IsGuardBroken: false,
            GuardStamina: 100,
            MaxGuardStamina: 100,
            SelectedHotbarSlot: 0,
            ImmutableSnapshotList<InventorySlotFrameSnapshot>.Empty);
    }
}
