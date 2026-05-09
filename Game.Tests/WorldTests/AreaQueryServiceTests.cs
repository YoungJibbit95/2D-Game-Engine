using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.World.Queries;
using System.Numerics;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class AreaQueryServiceTests
{
    [Fact]
    public void QueryEntities_CircleReturnsEntitiesInsideRadius()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var near = new PlayerEntity(new Vector2(10, 10), new TileCollisionResolver());
        var far = new PlayerEntity(new Vector2(90, 90), new TileCollisionResolver());
        entities.Add(near);
        entities.Add(far);

        var hits = new AreaQueryService().QueryEntities(
            entities,
            AreaQueryShape.Circle(new Vector2(16, 16), radius: 24));

        Assert.Contains(near, hits);
        Assert.DoesNotContain(far, hits);
    }

    [Fact]
    public void QueryEntities_ConeFiltersByDirectionAndRange()
    {
        var entities = new EntityManager(spatialCellSize: 16);
        var front = new PlayerEntity(new Vector2(40, 0), new TileCollisionResolver());
        var behind = new PlayerEntity(new Vector2(-40, 0), new TileCollisionResolver());
        entities.Add(front);
        entities.Add(behind);

        var hits = new AreaQueryService().QueryEntities(
            entities,
            AreaQueryShape.Cone(Vector2.Zero, Vector2.UnitX, radius: 80, angleRadians: MathF.PI / 2));

        Assert.Contains(front, hits);
        Assert.DoesNotContain(behind, hits);
    }
}
