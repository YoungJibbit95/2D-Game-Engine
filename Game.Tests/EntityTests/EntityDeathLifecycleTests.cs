using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Events;
using Game.Core.Loot;
using Game.Core.Physics;
using System.Numerics;
using Xunit;

namespace Game.Tests.EntityTests;

public sealed class EntityDeathLifecycleTests
{
    [Fact]
    public void ResolveDeath_MaterializesDeterministicLootExactlyOnce()
    {
        var collision = new TileCollisionResolver();
        var entities = new EntityManager();
        var victim = new EntityFactory(collision).CreateEnemy(new EntityDefinition
        {
            Id = "elite",
            DisplayName = "Elite",
            TexturePath = "entities/elite",
            MaxHealth = 1,
            LootTableId = "elite",
            Tags = new[] { "elite", "crystal" },
            Ai = new AiProfileDefinition { Kind = AiBehaviorKind.None }
        }, new Vector2(32, 32));
        entities.Add(victim);
        victim.ApplyDamage(new DamageInfo(1, DamageType.Melee, 99, Vector2.Zero, 0));
        var loot = LootTableRegistry.Create(new[]
        {
            new LootTableDefinition
            {
                Id = "elite",
                Entries = new[]
                {
                    new LootEntryDefinition { ItemId = "copper_coin", Min = 3, Max = 3, Guaranteed = true }
                }
            }
        });
        var lifecycle = new EntityDeathLifecycle(new LootRoller(new Random(999)), collision);
        var events = new GameEventBus();
        var lootEvents = new List<LootDroppedEvent>();
        events.Subscribe<LootDroppedEvent>(lootEvents.Add);
        var context = victim.CreateLootKillContext(EntityFaction.Friendly, true, 180);
        var key = new LootRollKey(42, 7, victim.Id);

        var first = lifecycle.ResolveDeath(victim, entities, loot, context, key, events);
        var duplicate = lifecycle.ResolveDeath(victim, entities, loot, context, key, events);

        Assert.True(first.Processed);
        Assert.Equal(1, first.DroppedStacks);
        Assert.False(duplicate.Processed);
        var drop = Assert.Single(entities.Entities.OfType<DroppedItemEntity>());
        Assert.Equal("copper_coin", drop.Stack.ItemId);
        Assert.Equal(3, drop.Stack.Count);
        var lootEvent = Assert.Single(lootEvents);
        Assert.Equal(victim.Id, lootEvent.VictimEntityId);
        Assert.Equal(drop.Stack, lootEvent.Stack);
        Assert.Equal(victim.Body.Position, lootEvent.WorldPosition);
    }
}
