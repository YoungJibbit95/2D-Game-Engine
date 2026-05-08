using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using InventoryModel = Game.Core.Inventory.Inventory;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Runtime;

public sealed class GameSimulation
{
    private readonly CombatSystem _combat;
    private readonly ItemPickupSystem _pickup;
    private readonly SpawnScheduler _spawnScheduler;
    private readonly SpawnSchedulerOptions _spawnOptions;
    private readonly PlayerRespawnSystem _respawn;

    public GameSimulation(
        GameContentDatabase content,
        GameWorld world,
        BiomeMap biomeMap,
        PlayerEntity player,
        InventoryModel playerInventory,
        EntityManager? entities = null,
        WorldTime? time = null,
        GameEventBus? events = null,
        CombatSystem? combat = null,
        ItemPickupSystem? pickup = null,
        SpawnScheduler? spawnScheduler = null,
        SpawnSchedulerOptions? spawnOptions = null,
        PlayerRespawnSystem? respawn = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Biomes = biomeMap ?? throw new ArgumentNullException(nameof(biomeMap));
        Player = player ?? throw new ArgumentNullException(nameof(player));
        PlayerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
        Entities = entities ?? new EntityManager();
        Time = time ?? new WorldTime();
        Events = events ?? new GameEventBus();
        _combat = combat ?? new CombatSystem(new LootRoller(new Random()), new TileCollisionResolver());
        _pickup = pickup ?? new ItemPickupSystem();
        _spawnScheduler = spawnScheduler ?? new SpawnScheduler();
        _spawnOptions = spawnOptions ?? SpawnSchedulerOptions.Default;
        _respawn = respawn ?? new PlayerRespawnSystem();
    }

    public GameContentDatabase Content { get; }

    public GameWorld World { get; }

    public BiomeMap Biomes { get; }

    public PlayerEntity Player { get; }

    public InventoryModel PlayerInventory { get; }

    public EntityManager Entities { get; }

    public WorldTime Time { get; }

    public GameEventBus Events { get; }

    public GameSimulationTickResult Tick(PlayerCommand command, float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return new GameSimulationTickResult(
                CombatResolutionResult.None,
                ContactDamageResult.None,
                0,
                SpawnSchedulerResult.None,
                PlayerRespawnResult.None);
        }

        Time.Update(deltaSeconds);
        if (!Player.HealthComponent.IsDead)
        {
            Player.SetCommand(command);
            Player.Update(World, deltaSeconds);
        }

        Entities.UpdateAll(World, deltaSeconds);

        var combat = _combat.ResolveProjectileHits(Entities, Content.LootTables, Events);
        var contactDamage = Player.HealthComponent.IsDead
            ? ContactDamageResult.None
            : _combat.ResolveEnemyContactDamage(Player, Entities, events: Events);
        var pickedUpItems = Player.HealthComponent.IsDead
            ? 0
            : _pickup.PickupItems(Entities, PlayerInventory, Player.Bounds.Inflate(12), Events);
        var spawning = _spawnScheduler.Update(
            World,
            Entities,
            Content,
            Biomes,
            Time,
            CoordinateUtils.WorldToTile(Player.Body.Center.X, Player.Body.Center.Y),
            deltaSeconds,
            _spawnOptions);
        var respawn = _respawn.Update(Player, World, deltaSeconds);

        return new GameSimulationTickResult(combat, contactDamage, pickedUpItems, spawning, respawn);
    }
}
