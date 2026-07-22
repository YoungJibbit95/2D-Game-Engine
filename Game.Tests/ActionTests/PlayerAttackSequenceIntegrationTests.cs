using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.ActionTests;

public sealed class PlayerAttackSequenceIntegrationTests
{
    private const float FixedDelta = 1f / 60f;

    [Fact]
    public void RangedSequence_SpendsAmmoAtAcceptedStartAndSpawnsOnlyInActiveWindow()
    {
        var sequence = CreateProjectileSequence(
            "bow.shot",
            new AttackResourceCost(Ammo: 1, AmmoItemId: "arrow"),
            startupTicks: 2,
            eventCapacity: 1);
        var weapon = CreateWeapon(
            "bow",
            ItemType.WeaponRanged,
            ItemActionKind.Shoot,
            sequence.Id,
            projectileId: "arrow_projectile");
        var ammo = CreateItem("arrow", ItemType.Ammo, maxStack: 99);
        var content = CreateContent([weapon, ammo], [sequence]);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("bow", 1));
        inventory.Hotbar.Slots[1].SetStack(new ItemStack("arrow", 1));
        using var simulation = CreateSimulation(content, player, inventory);
        var target = player.Body.Center + new Vector2(80, 0);

        var started = simulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, target));
        var startup = simulation.Tick(PlayerCommand.None, FixedDelta);
        var active = simulation.Tick(PlayerCommand.None, FixedDelta);

        Assert.True(started.ItemUse.Success);
        Assert.Equal(0, inventory.CountItem("arrow"));
        Assert.Equal(AttackRuntimePhase.Startup, started.Snapshot.Attack.Phase);
        Assert.Empty(started.Snapshot.Entities);
        Assert.Empty(startup.Snapshot.Entities);
        var projectile = Assert.Single(active.Snapshot.Entities);
        Assert.Equal(EntityFrameKind.Projectile, projectile.Kind);
        Assert.Equal(AttackRuntimePhase.Active, active.Snapshot.Attack.Phase);
        Assert.Single(active.Snapshot.Attack.Feedback);
        Assert.Equal(1, active.Snapshot.Attack.DroppedFeedback);
        Assert.Equal(AttackRuntimeEventKind.PhaseChanged, active.Snapshot.Attack.Feedback[0].Kind);

        Assert.Equal(AttackRuntimePhase.Startup, started.Snapshot.Attack.Phase);
        Assert.Single(started.Snapshot.Attack.Feedback);
        Assert.Equal(AttackRuntimeEventKind.AttackStarted, started.Snapshot.Attack.Feedback[0].Kind);
    }

    [Fact]
    public void RangedSequence_RejectsMissingAmmoWithoutStartingOrConsumingAnything()
    {
        var sequence = CreateProjectileSequence(
            "bow.shot",
            new AttackResourceCost(Ammo: 1, AmmoItemId: "arrow"),
            startupTicks: 1,
            eventCapacity: 8);
        var weapon = CreateWeapon(
            "bow",
            ItemType.WeaponRanged,
            ItemActionKind.Shoot,
            sequence.Id,
            projectileId: "arrow_projectile");
        var content = CreateContent([weapon, CreateItem("arrow", ItemType.Ammo, 99)], [sequence]);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("bow", 1));
        using var simulation = CreateSimulation(content, player, inventory);

        var result = simulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, player.Body.Center + Vector2.UnitX * 80));

        Assert.True(result.ItemUse.Blocked);
        Assert.Equal(Game.Core.Events.GameplayActionFailureReason.InsufficientAmmo, result.ItemUse.FailureReason);
        Assert.Equal(AttackRuntimePhase.Idle, result.Snapshot.Attack.Phase);
        Assert.Equal(0UL, result.Snapshot.Attack.AttackInstanceId);
        Assert.Empty(result.Snapshot.Entities);
        Assert.Contains(
            result.Snapshot.Attack.Feedback,
            feedback => feedback.Failure == AttackInputFailure.InsufficientAmmo);
    }

    [Fact]
    public void MagicSequence_UsesAuthoredManaCostAndRejectsBeforeStartWhenInsufficient()
    {
        var sequence = CreateProjectileSequence(
            "wand.cast",
            new AttackResourceCost(Mana: 6),
            startupTicks: 0,
            eventCapacity: 8);
        var weapon = CreateWeapon(
            "wand",
            ItemType.WeaponMagic,
            ItemActionKind.Cast,
            sequence.Id,
            projectileId: "magic_projectile") with
        {
            ManaCost = 20
        };
        var content = CreateContent([weapon], [sequence]);

        var acceptedPlayer = new PlayerEntity(
            new Vector2(32, 32),
            new TileCollisionResolver(),
            maxMana: 20,
            currentMana: 6);
        var acceptedInventory = new PlayerInventory(content.Items);
        acceptedInventory.Hotbar.Slots[0].SetStack(new ItemStack("wand", 1));
        using var acceptedSimulation = CreateSimulation(content, acceptedPlayer, acceptedInventory);
        var target = acceptedPlayer.Body.Center + Vector2.UnitX * 80;
        var accepted = acceptedSimulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, target));

        Assert.True(accepted.ItemUse.Success);
        Assert.Equal(0, acceptedPlayer.Mana);
        Assert.Equal(ManaSpendStatus.Spent, accepted.ItemUse.ManaSpend.Status);
        Assert.Equal(ManaReservationFinalizationStatus.Committed, accepted.ItemUse.ManaFinalization.Status);
        Assert.Equal(0, acceptedPlayer.ManaComponent.OpenReservationCount);
        Assert.Equal(DamageType.Magic, Assert.Single(accepted.Snapshot.Entities).DamageType);

        var rejectedPlayer = new PlayerEntity(
            new Vector2(32, 32),
            new TileCollisionResolver(),
            maxMana: 20,
            currentMana: 5);
        var rejectedInventory = new PlayerInventory(content.Items);
        rejectedInventory.Hotbar.Slots[0].SetStack(new ItemStack("wand", 1));
        using var rejectedSimulation = CreateSimulation(content, rejectedPlayer, rejectedInventory);
        var rejected = rejectedSimulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, rejectedPlayer.Body.Center + Vector2.UnitX * 80));

        Assert.True(rejected.ItemUse.Blocked);
        Assert.Equal(Game.Core.Events.GameplayActionFailureReason.InsufficientMana, rejected.ItemUse.FailureReason);
        Assert.Equal(5, rejectedPlayer.Mana);
        Assert.Equal(ManaSpendStatus.InsufficientMana, rejected.ItemUse.ManaSpend.Status);
        Assert.Equal(0, rejectedPlayer.ManaComponent.OpenReservationCount);
        Assert.Empty(rejected.Snapshot.Entities);
    }

    [Fact]
    public void MagicSequence_MaterializedWandProjectileAppliesMagicDamageThroughSimulation()
    {
        var sequence = CreateProjectileSequence(
            "wand.cast.damage",
            new AttackResourceCost(Mana: 6),
            startupTicks: 0,
            eventCapacity: 8);
        var weapon = CreateWeapon(
            "damage_wand",
            ItemType.WeaponMagic,
            ItemActionKind.Cast,
            sequence.Id,
            projectileId: "magic_projectile");
        var content = CreateContent([weapon], [sequence]);
        var player = new PlayerEntity(
            new Vector2(32, 32),
            new TileCollisionResolver(),
            maxMana: 20,
            currentMana: 6);
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack(weapon.Id, 1));
        var entities = new EntityManager(spatialCellSize: 16);
        var enemy = new EnemyEntity(
            "magic_target",
            new Vector2(41, 40),
            new Vector2(16, 16),
            new HealthComponent(20),
            NullAiBehavior.Instance,
            new TileCollisionResolver(),
            contactDamage: 0,
            movementMode: EntityMovementMode.Flying);
        entities.Add(enemy);
        using var simulation = CreateSimulation(content, player, inventory, entities);
        simulation.World.SetWall(2, 2, 1);

        var result = simulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(
                true,
                TilePos.Zero,
                player.Body.Center + Vector2.UnitX * 80));

        Assert.Equal(1, result.Combat.ProjectileHits);
        Assert.Equal(13, enemy.Health.Current);
        Assert.Equal(DamageType.Magic, enemy.LastDamage?.Type);
        Assert.Equal(0, player.Mana);
        Assert.Equal(ManaReservationFinalizationStatus.Committed, result.ItemUse.ManaFinalization.Status);
        Assert.Equal((ushort)1, simulation.World.GetTile(2, 2).WallId);
    }

    [Fact]
    public void MeleeSequence_UsesTimedSweepsDeduplicatesHitsAndAdvancesComboCosts()
    {
        var first = CreateMeleeStep(
            "slash-one",
            nextStepId: "slash-two",
            comboWindow: new AttackPhaseWindow(1, 5),
            staminaCost: 4);
        var second = CreateMeleeStep(
            "slash-two",
            nextStepId: null,
            comboWindow: null,
            staminaCost: 5);
        var sequence = new AttackSequenceDefinition
        {
            Id = "blade.combo",
            Steps = [first, second],
            InputBufferTicks = 3,
            EventCapacity = 32
        };
        var weapon = CreateWeapon(
            "blade",
            ItemType.WeaponMelee,
            ItemActionKind.Melee,
            sequence.Id,
            projectileId: null) with
        {
            Damage = 5,
            Knockback = 1
        };
        var content = CreateContent([weapon], [sequence]);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("blade", 1));
        var entities = new EntityManager();
        var enemy = new EnemyEntity(
            "target",
            new Vector2(62, 38),
            new Vector2(16, 16),
            new HealthComponent(50),
            NullAiBehavior.Instance,
            new TileCollisionResolver(),
            contactDamage: 0);
        entities.Add(enemy);
        var guard = new GuardRuntimeState(new GuardDefinition
        {
            MaxStamina = 20,
            StaminaRegenerationPerSecond = 0
        });
        using var simulation = CreateSimulation(content, player, inventory, entities, guard);
        var target = player.Body.Center + Vector2.UnitX * 80;

        var started = simulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, target));
        var firstActive = simulation.Tick(PlayerCommand.None, FixedDelta);
        var queued = simulation.Tick(
            PlayerCommand.None,
            FixedDelta,
            new PlayerItemUseRequest(true, TilePos.Zero, target));

        Assert.True(started.ItemUse.Success);
        Assert.Equal(16, guard.Stamina);
        Assert.Equal(45, enemy.Health.Current);
        Assert.True(queued.ItemUse.InProgress);
        Assert.True(queued.Snapshot.Attack.HasQueuedCombo);
        Assert.Equal(45, enemy.Health.Current);
        Assert.Contains(
            queued.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.HitRejectedDuplicate);

        simulation.Tick(PlayerCommand.None, FixedDelta);
        simulation.Tick(PlayerCommand.None, FixedDelta);
        var comboStarted = simulation.Tick(PlayerCommand.None, FixedDelta);

        Assert.Equal(1, comboStarted.Snapshot.Attack.ComboIndex);
        Assert.Equal("slash-two", comboStarted.Snapshot.Attack.StepId);
        Assert.Equal(11, guard.Stamina);
        Assert.Contains(
            comboStarted.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.ComboAdvanced);

        var secondActive = simulation.Tick(PlayerCommand.None, FixedDelta);

        Assert.Equal(45, enemy.Health.Current);
        Assert.Equal(AttackRuntimePhase.Active, secondActive.Snapshot.Attack.Phase);
        Assert.Contains(
            secondActive.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.HitAccepted);
        Assert.Equal(AttackRuntimePhase.Startup, started.Snapshot.Attack.Phase);
        Assert.Contains(
            started.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.AttackStarted);
        Assert.Equal(45, firstActive.Snapshot.Entities.Single(entity => entity.Id == enemy.Id).Health);
    }

    [Fact]
    public void ComboSequence_RejectsUnaffordableNextStepWithoutSpendingPartialResources()
    {
        var first = CreateMeleeStep(
            "slash-one",
            nextStepId: "slash-two",
            comboWindow: new AttackPhaseWindow(1, 5),
            staminaCost: 4);
        var second = CreateMeleeStep(
            "slash-two",
            nextStepId: null,
            comboWindow: null,
            staminaCost: 7);
        var sequence = new AttackSequenceDefinition
        {
            Id = "blade.expensive-combo",
            Steps = [first, second],
            EventCapacity = 32
        };
        var weapon = CreateWeapon(
            "blade",
            ItemType.WeaponMelee,
            ItemActionKind.Melee,
            sequence.Id,
            projectileId: null);
        var content = CreateContent([weapon], [sequence]);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("blade", 1));
        var guard = new GuardRuntimeState(new GuardDefinition
        {
            MaxStamina = 10,
            StaminaRegenerationPerSecond = 0
        });
        using var simulation = CreateSimulation(content, player, inventory, guard: guard);
        var request = new PlayerItemUseRequest(
            true,
            TilePos.Zero,
            player.Body.Center + Vector2.UnitX * 80);

        simulation.Tick(PlayerCommand.None, FixedDelta, request);
        simulation.Tick(PlayerCommand.None, FixedDelta);
        simulation.Tick(PlayerCommand.None, FixedDelta, request);
        simulation.Tick(PlayerCommand.None, FixedDelta);
        simulation.Tick(PlayerCommand.None, FixedDelta);
        var rejectedCombo = simulation.Tick(PlayerCommand.None, FixedDelta);

        Assert.Equal(6, guard.Stamina);
        Assert.Equal(1UL, rejectedCombo.Snapshot.Attack.AttackInstanceId);
        Assert.Equal(AttackRuntimePhase.Cooldown, rejectedCombo.Snapshot.Attack.Phase);
        Assert.Contains(
            rejectedCombo.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.InputRejected &&
                        feedback.Failure == AttackInputFailure.InsufficientStamina);
        Assert.Contains(
            rejectedCombo.Snapshot.Attack.Feedback,
            feedback => feedback.Kind == AttackRuntimeEventKind.ComboReset);
    }

    private static AttackSequenceDefinition CreateProjectileSequence(
        string id,
        AttackResourceCost cost,
        int startupTicks,
        int eventCapacity)
    {
        return new AttackSequenceDefinition
        {
            Id = id,
            Steps =
            [
                new AttackComboStepDefinition
                {
                    Id = "release",
                    Timeline = AttackTimelineDefinition.Create(startupTicks, 1, 2, 1),
                    Cost = cost
                }
            ],
            EventCapacity = eventCapacity
        };
    }

    private static AttackComboStepDefinition CreateMeleeStep(
        string id,
        string? nextStepId,
        AttackPhaseWindow? comboWindow,
        float staminaCost)
    {
        return new AttackComboStepDefinition
        {
            Id = id,
            Timeline = AttackTimelineDefinition.Create(
                1,
                2,
                2,
                cooldownTicks: 1,
                comboWindow: comboWindow),
            NextStepId = nextStepId,
            Cost = new AttackResourceCost(Stamina: staminaCost),
            MaxTargetsPerSwing = 4,
            MeleeShapes =
            [
                new SweptMeleeShapeDefinition
                {
                    Id = $"{id}.sweep",
                    ActiveEndTickExclusive = 2,
                    Sweep = new MeleeSweepDefinition
                    {
                        Reach = 36,
                        Radius = 12,
                        StartAngleRadians = -0.2f,
                        EndAngleRadians = 0.2f
                    }
                }
            ]
        };
    }

    private static ItemDefinition CreateWeapon(
        string id,
        ItemType type,
        ItemActionKind actionKind,
        string sequenceId,
        string? projectileId)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = id,
            Type = type,
            TexturePath = $"items/{id}",
            MaxStack = 1,
            Damage = 4,
            UseTime = 10,
            Actions =
            [
                new ItemActionDefinition
                {
                    Kind = actionKind,
                    AttackSequenceId = sequenceId,
                    ProjectileId = projectileId
                }
            ]
        };
    }

    private static ItemDefinition CreateItem(string id, ItemType type, int maxStack)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = id,
            Type = type,
            TexturePath = $"items/{id}",
            MaxStack = maxStack
        };
    }

    private static GameContentDatabase CreateContent(
        IReadOnlyList<ItemDefinition> items,
        IReadOnlyList<AttackSequenceDefinition> attackSequences)
    {
        return new GameContentDatabase(
            TileRegistry.Create(
            [
                new TileDefinition
                {
                    NumericId = 1,
                    Id = "dirt",
                    DisplayName = "Dirt",
                    TexturePath = "tiles/dirt",
                    Solid = true,
                    BlocksLight = true,
                    Hardness = 1
                }
            ]),
            ItemRegistry.Create(items),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(
            [
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "dirt",
                    UndergroundTile = "dirt"
                }
            ]),
            ProjectileRegistry.Create(
            [
                new ProjectileDefinition
                {
                    Id = "arrow_projectile",
                    TexturePath = "projectiles/arrow",
                    Speed = 200,
                    Damage = 3,
                    Lifetime = 2
                },
                new ProjectileDefinition
                {
                    Id = "magic_projectile",
                    TexturePath = "projectiles/magic",
                    Speed = 180,
                    Damage = 3,
                    Lifetime = 2
                }
            ]),
            EntityDefinitionRegistry.Create(Array.Empty<EntityDefinition>()),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()))
        {
            AttackSequences = AttackSequenceRegistry.Create(attackSequences)
        };
    }

    private static GameSimulation CreateSimulation(
        GameContentDatabase content,
        PlayerEntity player,
        PlayerInventory inventory,
        EntityManager? entities = null,
        GuardRuntimeState? guard = null)
    {
        return new GameSimulation(
            content,
            new World(32, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            player,
            inventory,
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            playerGuard: guard);
    }
}
