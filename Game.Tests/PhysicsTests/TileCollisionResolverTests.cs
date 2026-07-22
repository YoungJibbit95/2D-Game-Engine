using Game.Core;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.PhysicsTests;

public sealed class TileCollisionResolverTests
{
    [Fact]
    public void Move_FallingBodyLandsOnSolidTile()
    {
        var world = CreateWorld();
        world.SetTile(1, 10, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(GameConstants.TileSize, 9 * GameConstants.TileSize - 10),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(0, 100)
        };

        new TileCollisionResolver().Move(world, body, 1f);

        Assert.Equal(10 * GameConstants.TileSize - body.Size.Y, body.Position.Y);
        Assert.Equal(0, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void Move_BodyStopsAgainstSolidTileOnRight()
    {
        var world = CreateWorld();
        world.SetTile(5, 5, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 5 * GameConstants.TileSize),
            Size = new Vector2(16, 16),
            Velocity = new Vector2(100, 0)
        };

        new TileCollisionResolver().Move(world, body, 1f);

        Assert.Equal(5 * GameConstants.TileSize - body.Size.X, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void MoveDetailed_HighVelocityBodyCannotTunnelThroughSingleTileWall()
    {
        var world = CreateWorld();
        world.SetTile(20, 8, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(2 * GameConstants.TileSize, 8 * GameConstants.TileSize),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(4_000, 0)
        };
        Span<PhysicsContact> contacts = stackalloc PhysicsContact[2];

        var result = new TileCollisionResolver().MoveDetailed(world, body, 0.1f, contacts);

        Assert.Equal(20 * GameConstants.TileSize - body.Size.X, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
        Assert.True((result.ContactFlags & PhysicsContactFlags.RightWall) != 0);
        Assert.Equal(-Vector2.UnitX, contacts[0].Normal);
        Assert.Equal(20, contacts[0].TileX);
        Assert.InRange(result.Substeps, 1, TileCollisionSettings.Default.MaxSubsteps);
    }

    [Fact]
    public void MoveDetailed_ReportsGroundCeilingAndWallContactsWithoutAllocatingStorage()
    {
        var world = CreateWorld();
        world.SetTile(5, 4, KnownTileIds.Dirt);
        world.SetTile(4, 5, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 4 * GameConstants.TileSize),
            Size = new Vector2(16, 16),
            Velocity = new Vector2(64, 64)
        };
        Span<PhysicsContact> contacts = stackalloc PhysicsContact[4];

        var result = new TileCollisionResolver().MoveDetailed(world, body, 0.5f, contacts);

        Assert.True(result.Collided);
        Assert.True((result.ContactFlags & PhysicsContactFlags.RightWall) != 0);
        Assert.True((result.ContactFlags & PhysicsContactFlags.Ground) != 0);
        Assert.Equal(2, result.ContactsFound);
        Assert.Equal(2, result.ContactsWritten);
        Assert.True(body.OnGround);
        Assert.Equal(Vector2.Zero, body.Velocity);
    }

    [Fact]
    public void MoveDetailed_WorldLayerCanBeExcludedForSensors()
    {
        var world = CreateWorld();
        world.SetTile(5, 5, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 5 * GameConstants.TileSize),
            Size = new Vector2(16, 16),
            Velocity = new Vector2(100, 0),
            CollisionLayer = PhysicsCollisionLayer.Sensor,
            CollisionMask = PhysicsCollisionLayer.Player
        };

        var result = new TileCollisionResolver().MoveDetailed(
            world,
            body,
            1f,
            Span<PhysicsContact>.Empty);

        Assert.False(result.Collided);
        Assert.Equal(4 * GameConstants.TileSize + 100, body.Position.X);
        Assert.Equal(100, body.Velocity.X);
        Assert.Equal(0, result.TilesTested);
    }

    [Fact]
    public void MoveDetailed_FailsClosedWhenTileWorkBudgetIsExhausted()
    {
        var world = CreateWorld();
        world.GetOrCreateChunk(new ChunkPos(0, 0));
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 4 * GameConstants.TileSize),
            Size = new Vector2(32, 32),
            Velocity = new Vector2(1_000, 0)
        };
        var resolver = new TileCollisionResolver(new TileCollisionSettings(16f, 1, 1));

        var result = resolver.MoveDetailed(world, body, 1f, Span<PhysicsContact>.Empty);

        Assert.True(result.WorkBudgetExhausted);
        Assert.Equal(4 * GameConstants.TileSize, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
        Assert.Equal(1, result.TilesTested);
    }

    [Fact]
    public void Move_JumpingRightAtNegativeCoordinatesDoesNotEnterSolidWall()
    {
        var world = new World(
            GameConstants.ChunkSize,
            256,
            WorldMetadata.CreateDefault(seed: 1),
            isHorizontallyInfinite: true);
        world.SetTile(-2156, 107, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(-34508f, 1718f),
            Size = new Vector2(12f, 10f),
            Velocity = new Vector2(42f, -217.5f)
        };

        new TileCollisionResolver().Move(world, body, 1f / 60f);

        Assert.Equal(-34508f, body.Position.X);
        Assert.Equal(0f, body.Velocity.X);
        Assert.Equal(-217.5f, body.Velocity.Y);
    }

    [Fact]
    public void MoveDetailed_RecoversSquirrelSizedInitialOverlapAtNegativeCoordinates()
    {
        const int solidTileX = -2_215;
        const int solidTileY = 90;
        var world = new World(
            GameConstants.ChunkSize,
            256,
            WorldMetadata.CreateDefault(seed: 1),
            isHorizontallyInfinite: true);
        world.SetTile(solidTileX, solidTileY, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(-35_436.1436f, solidTileY * GameConstants.TileSize + 5f),
            Size = new Vector2(12f, 10f),
            Velocity = new Vector2(0f, 122.5f)
        };
        Span<PhysicsContact> contacts = stackalloc PhysicsContact[2];

        var result = new TileCollisionResolver().MoveDetailed(
            world,
            body,
            0f,
            contacts);

        Assert.True((result.ContactFlags & PhysicsContactFlags.InitialOverlapRecovered) != 0);
        Assert.False((result.ContactFlags & PhysicsContactFlags.InitialOverlapUnresolved) != 0);
        Assert.Equal(1, result.ContactsFound);
        Assert.Equal(1, result.ContactsWritten);
        Assert.NotEqual(Vector2.Zero, result.ActualDisplacement);
        AssertBodyDoesNotIntersectSolidTile(world, body);
    }

    [Fact]
    public void MoveDetailed_DepenetratesAcrossOverlappedTileUnionDeterministically()
    {
        var world = CreateWorld();
        world.SetTile(8, 8, KnownTileIds.Dirt);
        world.SetTile(9, 8, KnownTileIds.Dirt);

        var initialPosition = new Vector2(
            8 * GameConstants.TileSize + 10,
            8 * GameConstants.TileSize + 3);
        var first = new PhysicsBody
        {
            Position = initialPosition,
            Size = new Vector2(20, 10),
            Velocity = new Vector2(15, 25)
        };
        var second = new PhysicsBody
        {
            Position = initialPosition,
            Size = new Vector2(20, 10),
            Velocity = new Vector2(15, 25)
        };
        var resolver = new TileCollisionResolver();

        var firstResult = resolver.MoveDetailed(world, first, 0f, Span<PhysicsContact>.Empty);
        var secondResult = resolver.MoveDetailed(world, second, 0f, Span<PhysicsContact>.Empty);

        Assert.True((firstResult.ContactFlags & PhysicsContactFlags.InitialOverlapRecovered) != 0);
        Assert.Equal(first.Position, second.Position);
        Assert.Equal(firstResult.ContactFlags, secondResult.ContactFlags);
        AssertBodyDoesNotIntersectSolidTile(world, first);
    }

    [Fact]
    public void MoveDetailed_ReportsUnresolvedInitialOverlapWhenRecoveryBudgetIsInsufficient()
    {
        var world = CreateWorld();
        world.SetTile(4, 4, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize + 2, 4 * GameConstants.TileSize + 2),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(100, 100)
        };
        var resolver = new TileCollisionResolver(new TileCollisionSettings(16f, 1, 1));

        var result = resolver.MoveDetailed(world, body, 1f / 60f, Span<PhysicsContact>.Empty);

        Assert.True(result.WorkBudgetExhausted);
        Assert.True((result.ContactFlags & PhysicsContactFlags.InitialOverlapUnresolved) != 0);
        Assert.Equal(Vector2.Zero, body.Velocity);
        Assert.Equal(1, result.TilesTested);
    }

    [Fact]
    public void MoveDetailed_LandsOnOneWayPlatformFromAbove()
    {
        var world = CreateWorld();
        world.SetTile(1, 10, TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.Platform));
        var body = new PhysicsBody
        {
            Position = new Vector2(GameConstants.TileSize, 9 * GameConstants.TileSize),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(0, 120)
        };

        new TileCollisionResolver().Move(world, body, 1f);

        Assert.Equal(10 * GameConstants.TileSize - body.Size.Y, body.Position.Y);
        Assert.Equal(0, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void MoveDetailed_PassesThroughOneWayPlatformWhenMovingUpOrSideways()
    {
        var world = CreateWorld();
        world.SetTile(1, 10, TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.Platform));

        var movingUp = new PhysicsBody
        {
            Position = new Vector2(GameConstants.TileSize, 11 * GameConstants.TileSize + 1f),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(0, -120)
        };
        new TileCollisionResolver().Move(world, movingUp, 1f);

        Assert.Equal(11 * GameConstants.TileSize + 1f - 120f, movingUp.Position.Y);

        var movingSideways = new PhysicsBody
        {
            Position = new Vector2(0f, 10 * GameConstants.TileSize + 5f),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(120, 0)
        };
        new TileCollisionResolver().Move(world, movingSideways, 1f);

        Assert.Equal(120f, movingSideways.Position.X);
    }

    [Fact]
    public void MoveDetailed_InitialPlatformOverlapDoesNotDepenetrateOrCancelUpwardMotion()
    {
        var world = CreateWorld();
        world.SetTile(1, 10, TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.Platform));
        var body = new PhysicsBody
        {
            Position = new Vector2(GameConstants.TileSize, 10 * GameConstants.TileSize + 4f),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(0, -40)
        };
        var contacts = new PhysicsContact[4];

        var result = new TileCollisionResolver().MoveDetailed(world, body, 1f, contacts);

        Assert.Equal(10 * GameConstants.TileSize - 36f, body.Position.Y, precision: 3);
        Assert.Equal(-40f, body.Velocity.Y);
        Assert.False(body.OnGround);
        Assert.Equal(
            PhysicsContactFlags.None,
            result.ContactFlags &
            (PhysicsContactFlags.InitialOverlapRecovered | PhysicsContactFlags.InitialOverlapUnresolved));
        Assert.Equal(0, result.ContactsWritten);
    }

    [Theory]
    [InlineData(TileFlags.Slope, TileCollisionShape.SlopeAscendingRight)]
    [InlineData(TileFlags.SlopeAscendingRight, TileCollisionShape.SlopeAscendingRight)]
    [InlineData(TileFlags.SlopeAscendingLeft, TileCollisionShape.SlopeAscendingLeft)]
    [InlineData(TileFlags.HalfBlock, TileCollisionShape.HalfBlock)]
    [InlineData(TileFlags.Platform, TileCollisionShape.OneWayPlatform)]
    public void TileInstance_ResolvesPartialCollisionShapeContract(
        TileFlags flags,
        TileCollisionShape expected)
    {
        var tile = TileInstance.FromTileId(KnownTileIds.Dirt, flags);

        Assert.Equal(expected, tile.CollisionShape);
    }

    [Fact]
    public void TileInstance_AmbiguousSlopeOrientationFailsClosedAsFullBlock()
    {
        var tile = TileInstance.FromTileId(
            KnownTileIds.Dirt,
            TileFlags.SlopeAscendingLeft | TileFlags.SlopeAscendingRight);

        Assert.Equal(TileCollisionShape.FullBlock, tile.CollisionShape);
    }

    [Theory]
    [InlineData(TileFlags.SlopeAscendingRight, 160f, -0.70710677f)]
    [InlineData(TileFlags.SlopeAscendingLeft, 152f, 0.70710677f)]
    [InlineData(TileFlags.HalfBlock, 160f, 0f)]
    public void MoveDetailed_FallingBodyLandsOnAuthoredPartialFloor(
        TileFlags flags,
        float expectedY,
        float expectedNormalX)
    {
        var world = CreateWorld();
        world.SetTile(4, 10, TileInstance.FromTileId(KnownTileIds.Dirt, flags));
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 140f),
            Size = new Vector2(8f, 8f),
            Velocity = new Vector2(0f, 40f)
        };
        Span<PhysicsContact> contacts = stackalloc PhysicsContact[8];

        var result = new TileCollisionResolver().MoveDetailed(world, body, 1f, contacts);

        Assert.Equal(expectedY, body.Position.Y, precision: 3);
        Assert.Equal(0f, body.Velocity.Y);
        Assert.True(body.OnGround);
        Assert.True((result.ContactFlags & PhysicsContactFlags.Ground) != 0);
        Assert.True(result.ContactsWritten >= 1);
        Assert.Equal(expectedNormalX, contacts[0].Normal.X, precision: 5);
        Assert.True(contacts[0].Normal.Y < -0.7f || expectedNormalX == 0f);
    }

    [Fact]
    public void MoveDetailed_HorizontalMotionFollowsAscendingSlopeWithoutPenetration()
    {
        var world = CreateWorld();
        world.SetTile(
            4,
            10,
            TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.SlopeAscendingRight));
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 168f),
            Size = new Vector2(4f, 4f),
            Velocity = new Vector2(8f, 0f)
        };

        var result = new TileCollisionResolver().MoveDetailed(
            world,
            body,
            0.5f,
            Span<PhysicsContact>.Empty);

        Assert.Equal(4 * GameConstants.TileSize + 4f, body.Position.X, precision: 3);
        Assert.Equal(164f, body.Position.Y, precision: 3);
        Assert.True(body.OnGround);
        Assert.True((result.ContactFlags & PhysicsContactFlags.Ground) != 0);
    }

    [Fact]
    public void MoveDetailed_HighSideOfSlopeRemainsAHorizontalBarrier()
    {
        var world = CreateWorld();
        world.SetTile(
            4,
            10,
            TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.SlopeAscendingRight));
        var body = new PhysicsBody
        {
            Position = new Vector2(5 * GameConstants.TileSize, 160f),
            Size = new Vector2(4f, 4f),
            Velocity = new Vector2(-16f, 0f)
        };

        var result = new TileCollisionResolver().MoveDetailed(
            world,
            body,
            0.5f,
            Span<PhysicsContact>.Empty);

        Assert.Equal(5 * GameConstants.TileSize, body.Position.X);
        Assert.Equal(0f, body.Velocity.X);
        Assert.True((result.ContactFlags & PhysicsContactFlags.LeftWall) != 0);
    }

    [Fact]
    public void MoveDetailed_UpwardMotionPassesThroughFloorSlope()
    {
        var world = CreateWorld();
        world.SetTile(
            4,
            10,
            TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.SlopeAscendingLeft));
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 176f),
            Size = new Vector2(8f, 8f),
            Velocity = new Vector2(0f, -32f)
        };

        var result = new TileCollisionResolver().MoveDetailed(
            world,
            body,
            1f,
            Span<PhysicsContact>.Empty);

        Assert.Equal(144f, body.Position.Y, precision: 3);
        Assert.Equal(-32f, body.Velocity.Y);
        Assert.False(result.Collided);
    }

    [Fact]
    public void MoveDetailed_PartialFloorSupportReusesCallerStorageWithoutAllocation()
    {
        var world = CreateWorld();
        world.SetTile(
            4,
            10,
            TileInstance.FromTileId(KnownTileIds.Dirt, TileFlags.SlopeAscendingRight));
        var resolver = new TileCollisionResolver();
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 140f),
            Size = new Vector2(8f, 8f),
            Velocity = new Vector2(0f, 40f)
        };
        Span<PhysicsContact> contacts = stackalloc PhysicsContact[8];
        _ = resolver.MoveDetailed(world, body, 1f, contacts);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            body.Position = new Vector2(4 * GameConstants.TileSize, 140f);
            body.Velocity = new Vector2(0f, 40f);
            _ = resolver.MoveDetailed(world, body, 1f, contacts);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertBodyDoesNotIntersectSolidTile(World world, PhysicsBody body)
    {
        var minTileX = (int)Math.Floor(body.Position.X / GameConstants.TileSize);
        var minTileY = (int)Math.Floor(body.Position.Y / GameConstants.TileSize);
        var maxTileX = (int)Math.Floor(
            (body.Position.X + body.Size.X - 0.01f) / GameConstants.TileSize);
        var maxTileY = (int)Math.Floor(
            (body.Position.Y + body.Size.Y - 0.01f) / GameConstants.TileSize);
        for (var tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            for (var tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                Assert.True(world.TryGetTile(tileX, tileY, out var tile));
                Assert.False(tile.IsSolid, $"Body still overlaps solid tile ({tileX},{tileY}).");
            }
        }
    }

    private static World CreateWorld()
    {
        return new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
    }
}
