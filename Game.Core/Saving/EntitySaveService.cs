using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Inventory;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Saving;

public sealed class EntitySaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TileCollisionResolver _collisionResolver;

    public EntitySaveService()
        : this(new TileCollisionResolver())
    {
    }

    public EntitySaveService(TileCollisionResolver collisionResolver)
    {
        _collisionResolver = collisionResolver;
    }

    public void Save(IEnumerable<Entity> entities, string filePath)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = entities
            .Where(entity => entity.IsActive)
            .Select(ToSaveData)
            .Where(data => data is not null)
            .ToArray();

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public IReadOnlyList<Entity> Load(string filePath, GameContentDatabase content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        if (!File.Exists(filePath))
        {
            return Array.Empty<Entity>();
        }

        var data = JsonSerializer.Deserialize<RuntimeEntitySaveData[]>(File.ReadAllText(filePath), Options)
            ?? Array.Empty<RuntimeEntitySaveData>();

        return data.Select(item => FromSaveData(item, content)).ToArray();
    }

    private static RuntimeEntitySaveData? ToSaveData(Entity entity)
    {
        return entity switch
        {
            EnemyEntity enemy => new RuntimeEntitySaveData
            {
                Kind = RuntimeEntityKind.Enemy,
                RuntimeId = enemy.Id,
                DefinitionId = enemy.DefinitionId,
                PositionX = enemy.Body.Position.X,
                PositionY = enemy.Body.Position.Y,
                VelocityX = enemy.Body.Velocity.X,
                VelocityY = enemy.Body.Velocity.Y,
                Health = enemy.Health.Current
            },
            ProjectileEntity projectile => new RuntimeEntitySaveData
            {
                Kind = RuntimeEntityKind.Projectile,
                RuntimeId = projectile.Id,
                DefinitionId = projectile.ProjectileId,
                PositionX = projectile.Position.X,
                PositionY = projectile.Position.Y,
                VelocityX = projectile.Velocity.X,
                VelocityY = projectile.Velocity.Y,
                Age = projectile.Age,
                Pierce = projectile.Pierce,
                OwnerEntityId = projectile.OwnerEntityId
            },
            DroppedItemEntity droppedItem => new RuntimeEntitySaveData
            {
                Kind = RuntimeEntityKind.DroppedItem,
                RuntimeId = droppedItem.Id,
                DefinitionId = droppedItem.Stack.ItemId,
                PositionX = droppedItem.Body.Position.X,
                PositionY = droppedItem.Body.Position.Y,
                VelocityX = droppedItem.Body.Velocity.X,
                VelocityY = droppedItem.Body.Velocity.Y,
                StackCount = droppedItem.Stack.Count
            },
            _ => null
        };
    }

    private Entity FromSaveData(RuntimeEntitySaveData data, GameContentDatabase content)
    {
        Entity entity = data.Kind switch
        {
            RuntimeEntityKind.Enemy => RestoreEnemy(data, content),
            RuntimeEntityKind.Projectile => RestoreProjectile(data, content),
            RuntimeEntityKind.DroppedItem => RestoreDroppedItem(data, content),
            _ => throw new InvalidDataException($"Unknown runtime entity kind '{data.Kind}'.")
        };

        entity.AssignId(data.RuntimeId);
        return entity;
    }

    private EnemyEntity RestoreEnemy(RuntimeEntitySaveData data, GameContentDatabase content)
    {
        var definition = content.Entities.GetById(data.DefinitionId);
        var enemy = new EntityFactory(_collisionResolver).CreateEnemy(
            definition,
            new Vector2(data.PositionX, data.PositionY),
            currentHealth: data.Health);
        enemy.Body.Velocity = new Vector2(data.VelocityX, data.VelocityY);
        return enemy;
    }

    private static ProjectileEntity RestoreProjectile(RuntimeEntitySaveData data, GameContentDatabase content)
    {
        var definition = content.Projectiles.GetById(data.DefinitionId);
        return new ProjectileEntity(
            definition.Id,
            new Vector2(data.PositionX, data.PositionY),
            new Vector2(data.VelocityX, data.VelocityY),
            definition.Damage,
            definition.Gravity,
            data.Pierce,
            definition.Lifetime,
            data.OwnerEntityId,
            data.Age);
    }

    private DroppedItemEntity RestoreDroppedItem(RuntimeEntitySaveData data, GameContentDatabase content)
    {
        if (!content.Items.TryGetById(data.DefinitionId, out _))
        {
            throw new InvalidDataException($"Saved dropped item references unknown item '{data.DefinitionId}'.");
        }

        if (data.StackCount <= 0)
        {
            throw new InvalidDataException($"Saved dropped item '{data.DefinitionId}' has invalid stack count {data.StackCount}.");
        }

        var droppedItem = new DroppedItemEntity(
            new ItemStack(data.DefinitionId, data.StackCount),
            new Vector2(data.PositionX, data.PositionY),
            _collisionResolver);
        droppedItem.Body.Velocity = new Vector2(data.VelocityX, data.VelocityY);
        return droppedItem;
    }
}
