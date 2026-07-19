using System.Numerics;
using Game.Core.Entities;
using Game.Core.Movement;
using Game.Core.Physics;
using Game.Core.World;
using Xunit;

namespace Game.Tests.MovementTests;

public sealed class SideViewCharacterControllerTests
{
    private const float FixedDeltaSeconds = 1f / 60f;

    [Fact]
    public void ApplyIntent_TranslatesInputWithoutOwningGravityOrCollision()
    {
        var world = CreateGroundedWorld();
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 1f, WantsJump: true),
            FixedDeltaSeconds);

        Assert.Equal(43.333f, body.Velocity.X, precision: 3);
        Assert.Equal(-365f, body.Velocity.Y, precision: 3);
        Assert.False(body.OnGround);
        Assert.Equal(new Vector2(32, 20), body.Position);
    }

    [Fact]
    public void ApplyIntent_UsesGroundDecelerationWithoutAdvancingPhysics()
    {
        var world = CreateGroundedWorld();
        var body = CreateGroundedBody();
        body.Velocity = new Vector2(100f, 0f);
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(70f, body.Velocity.X, precision: 3);
        Assert.Equal(0f, body.Velocity.Y);
        Assert.True(body.OnGround);
        Assert.Equal(new Vector2(32, 20), body.Position);
    }

    [Fact]
    public void ApplyIntent_CoyoteTimeAcceptsJumpForSixTicksAfterLeavingGround()
    {
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        body.OnGround = false;
        for (var airborneTick = 0; airborneTick < 5; airborneTick++)
        {
            controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        }

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);

        Assert.Equal(-365f, body.Velocity.Y);
        Assert.False(body.OnGround);
    }

    [Fact]
    public void ApplyIntent_CoyoteTimeExpiresAfterSixAirborneTicksAtSixtyHertz()
    {
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        body.OnGround = false;
        for (var airborneTick = 0; airborneTick < 6; airborneTick++)
        {
            controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        }

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);

        Assert.Equal(0f, body.Velocity.Y);
        Assert.False(body.OnGround);
    }

    [Fact]
    public void ApplyIntent_BufferedTapStartsShortJumpOnLandingAfterButtonRelease()
    {
        var body = CreateGroundedBody();
        body.OnGround = false;
        body.Velocity = new Vector2(0f, 120f);
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        body.OnGround = true;
        body.Velocity = Vector2.Zero;

        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(-164.25f, body.Velocity.Y, precision: 3);
        Assert.False(body.OnGround);
    }

    [Fact]
    public void ApplyIntent_BufferedHeldJumpStartsFullJumpOnLanding()
    {
        var body = CreateGroundedBody();
        body.OnGround = false;
        body.Velocity = new Vector2(0f, 120f);
        var controller = new SideViewCharacterController();
        var heldJump = new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true);

        controller.ApplyIntent(body, heldJump, FixedDeltaSeconds);
        body.OnGround = true;
        body.Velocity = Vector2.Zero;

        controller.ApplyIntent(body, heldJump, FixedDeltaSeconds);

        Assert.Equal(-365f, body.Velocity.Y);
        Assert.False(body.OnGround);
    }

    [Fact]
    public void ApplyIntent_ExpiredJumpBufferDoesNotAutoJumpOnLanding()
    {
        var body = CreateGroundedBody();
        body.OnGround = false;
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        for (var airborneTick = 0; airborneTick < 7; airborneTick++)
        {
            controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);
        }

        body.OnGround = true;
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(0f, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void ApplyIntent_ReleasingJumpEarlyCutsOnlyTheOwnedUpwardImpulse()
    {
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(-164.25f, body.Velocity.Y, precision: 3);

        body.Velocity = new Vector2(0f, -200f);
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(-200f, body.Velocity.Y);
    }

    [Fact]
    public void ApplyIntent_HoldingJumpDoesNotRepeatWhenGroundContactReturns()
    {
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();
        var heldJump = new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true);

        controller.ApplyIntent(body, heldJump, FixedDeltaSeconds);
        body.OnGround = true;
        body.Velocity = Vector2.Zero;
        controller.ApplyIntent(body, heldJump, FixedDeltaSeconds);

        Assert.Equal(0f, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void ControllerAndPhysicsWorld_ProduceDistinctShortAndFullJumpArcsAtSixtyHertz()
    {
        var shortJumpHeight = MeasureJumpHeight(releaseAfterTicks: 3);
        var fullJumpHeight = MeasureJumpHeight(releaseAfterTicks: 120);

        Assert.InRange(shortJumpHeight, 24f, 26f);
        Assert.InRange(fullJumpHeight, 60f, 70f);
        Assert.True(fullJumpHeight - shortJumpHeight >= 30f);
    }

    [Fact]
    public void Reset_DiscardsBufferedInputAcrossRespawnBoundaries()
    {
        var body = CreateGroundedBody();
        body.OnGround = false;
        var controller = new SideViewCharacterController();

        controller.ApplyIntent(
            body,
            new SideViewCharacterInput(MoveAxis: 0f, WantsJump: true),
            FixedDeltaSeconds);
        controller.Reset();
        body.OnGround = true;
        body.Velocity = Vector2.Zero;
        controller.ApplyIntent(body, SideViewCharacterInput.None, FixedDeltaSeconds);

        Assert.Equal(0f, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void Move_FixedStepTraceIsDeterministic()
    {
        var world = CreateGroundedWorld(width: 256);
        var first = CreateGroundedBody();
        var second = CreateGroundedBody();
        var firstController = new SideViewCharacterController();
        var secondController = new SideViewCharacterController();

        for (var tick = 0; tick < 600; tick++)
        {
            var input = new SideViewCharacterInput(
                MoveAxis: tick < 300 ? 0.75f : -0.5f,
                WantsJump: tick is 30 or 330);
            firstController.ApplyIntent(first, input, FixedDeltaSeconds);
            secondController.ApplyIntent(second, input, FixedDeltaSeconds);
        }

        Assert.Equal(first.Position, second.Position);
        Assert.Equal(first.Velocity, second.Velocity);
        Assert.Equal(first.OnGround, second.OnGround);
    }

    [Fact]
    public void Move_SteadyStateAllocatesNoManagedMemory()
    {
        var world = CreateGroundedWorld(width: 256);
        var body = CreateGroundedBody();
        var controller = new SideViewCharacterController();
        var input = new SideViewCharacterInput(MoveAxis: 1f, WantsJump: false);

        for (var tick = 0; tick < 32; tick++)
        {
            controller.ApplyIntent(body, input, FixedDeltaSeconds);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < 1024; tick++)
        {
            controller.ApplyIntent(body, input, FixedDeltaSeconds);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Constructor_RejectsInvalidTuningBeforeHotPath()
    {
        var options = SideViewCharacterControllerOptions.Default with
        {
            GroundAcceleration = float.NaN
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SideViewCharacterController(options));
    }

    [Fact]
    public void Constructor_RejectsInvalidJumpGraceAndReleaseTuning()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SideViewCharacterController(SideViewCharacterControllerOptions.Default with
            {
                CoyoteTimeSeconds = float.PositiveInfinity
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SideViewCharacterController(SideViewCharacterControllerOptions.Default with
            {
                JumpBufferSeconds = -0.01f
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SideViewCharacterController(SideViewCharacterControllerOptions.Default with
            {
                JumpReleaseVelocityMultiplier = 1.01f
            }));
    }

    [Fact]
    public void PlayerEntity_UsesControllerIntentWithDynamicPhysicsBody()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());

        Assert.Equal(PhysicsBodyType.Dynamic, player.Body.BodyType);
        Assert.Equal(PhysicsCollisionLayer.Player, player.Body.CollisionLayer);
    }

    private static World CreateGroundedWorld(int width = 32)
    {
        var world = new World(width, 8, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < width; x++)
        {
            world.SetTile(x, 3, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        }

        return world;
    }

    private static PhysicsBody CreateGroundedBody()
    {
        return new PhysicsBody
        {
            Position = new Vector2(32, 20),
            Size = new Vector2(12, 28),
            OnGround = true,
            BodyType = PhysicsBodyType.Kinematic,
            CollisionLayer = PhysicsCollisionLayer.Player
        };
    }

    private static float MeasureJumpHeight(int releaseAfterTicks)
    {
        var world = new World(32, 16, WorldMetadata.CreateDefault(seed: 1));
        const int groundTileY = 10;
        for (var x = 0; x < 32; x++)
        {
            world.SetTile(x, groundTileY, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        }

        var body = new PhysicsBody
        {
            Position = new Vector2(32f, groundTileY * 16f - 28f),
            Size = new Vector2(12f, 28f),
            OnGround = true,
            BodyType = PhysicsBodyType.Dynamic,
            CollisionLayer = PhysicsCollisionLayer.Player
        };
        var controller = new SideViewCharacterController();
        var physics = new PhysicsWorld(
            settings: new PhysicsStepSettings(
                new Vector2(0f, 1050f),
                1f / 15f,
                620f,
                1,
                4));
        PhysicsBody[] bodies = [body];
        var results = new PhysicsMoveResult[1];
        var contacts = new PhysicsContact[4];
        var startY = body.Position.Y;
        var minimumY = startY;

        for (var tick = 0; tick < 120; tick++)
        {
            controller.ApplyIntent(
                body,
                new SideViewCharacterInput(MoveAxis: 0f, WantsJump: tick < releaseAfterTicks),
                FixedDeltaSeconds);
            physics.Step(world, bodies, FixedDeltaSeconds, results, contacts);
            minimumY = Math.Min(minimumY, body.Position.Y);
            if (tick > 0 && body.Velocity.Y >= 0f)
            {
                break;
            }
        }

        return startY - minimumY;
    }
}
