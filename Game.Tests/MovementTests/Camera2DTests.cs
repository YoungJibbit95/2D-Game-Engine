using Game.Client.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.MovementTests;

public sealed class Camera2DTests
{
    private const float DeltaSeconds = 1f / 60f;
    private static readonly Rectangle Viewport = new(0, 0, 1280, 720);

    [Fact]
    public void Follow_FirstTargetSnapsWithoutStartupDrift()
    {
        var camera = new Camera2D();
        var target = new Vector2(320f, 180f);

        camera.Follow(target, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        Assert.Equal(target, camera.Position);
        Assert.Equal(Vector2.Zero, camera.Velocity);
        Assert.InRange((int)target.X, camera.VisibleWorldRect.Left, camera.VisibleWorldRect.Right);
        Assert.InRange((int)target.Y, camera.VisibleWorldRect.Top, camera.VisibleWorldRect.Bottom);
    }

    [Fact]
    public void Follow_DeadZoneIgnoresSmallMovementInputNoise()
    {
        var camera = new Camera2D();
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        camera.Follow(new Vector2(12f, 8f), Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        Assert.Equal(Vector2.Zero, camera.Position);
        Assert.Equal(Vector2.Zero, camera.Velocity);
    }

    [Fact]
    public void Follow_CriticallyDampedMotionConvergesWithoutSnappingToNormalMovement()
    {
        var camera = new Camera2D();
        var target = new Vector2(220f, 0f);
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        camera.Follow(target, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        Assert.InRange(camera.Position.X, 0.01f, 20f);
        Assert.True(camera.Velocity.X > 0f);

        for (var frame = 0; frame < 180; frame++)
        {
            camera.Follow(target, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
            Assert.InRange(camera.Position.X, 0f, target.X + 24f);
        }

        Assert.InRange(camera.Position.X, 199f, 201f);
    }

    [Fact]
    public void Follow_LookAheadIsDirectionAwareAndStrictlyBounded()
    {
        var camera = new Camera2D();
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        for (var frame = 1; frame <= 20; frame++)
        {
            camera.Follow(
                new Vector2(frame * 100f, 0f),
                Viewport,
                smoothing: 0.2f,
                deltaSeconds: DeltaSeconds,
                lookAheadLimitPixels: 48f);
            Assert.InRange(camera.HorizontalLookAhead, 0f, 48f);
        }

        Assert.True(camera.HorizontalLookAhead > 40f);
    }

    [Fact]
    public void Follow_ConfiguredLookAheadIsClampedAndCanBeDisabled()
    {
        var camera = new Camera2D();
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        for (var frame = 1; frame <= 40; frame++)
        {
            camera.Follow(
                new Vector2(frame * 100f, 0f),
                Viewport,
                smoothing: 0.2f,
                deltaSeconds: DeltaSeconds,
                lookAheadLimitPixels: 10_000f);
            Assert.InRange(camera.HorizontalLookAhead, 0f, 256f);
        }

        Assert.True(camera.HorizontalLookAhead > 250f);

        camera.Follow(
            new Vector2(4_100f, 0f),
            Viewport,
            smoothing: 0.2f,
            deltaSeconds: DeltaSeconds,
            lookAheadLimitPixels: 8f);
        Assert.InRange(camera.HorizontalLookAhead, 0f, 8f);

        camera.Follow(
            new Vector2(4_200f, 0f),
            Viewport,
            smoothing: 0.2f,
            deltaSeconds: DeltaSeconds,
            lookAheadLimitPixels: 0f);
        Assert.Equal(0f, camera.HorizontalLookAhead);
    }

    [Fact]
    public void Follow_TeleportSnapsAndClearsCameraVelocity()
    {
        var camera = new Camera2D();
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
        camera.Follow(new Vector2(120f, 0f), Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
        Assert.NotEqual(Vector2.Zero, camera.Velocity);

        var teleportedTarget = new Vector2(900f, 300f);
        camera.Follow(teleportedTarget, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);

        Assert.Equal(teleportedTarget, camera.Position);
        Assert.Equal(Vector2.Zero, camera.Velocity);
        Assert.Equal(0f, camera.HorizontalLookAhead);
    }

    [Fact]
    public void Follow_SteadyStateAllocatesNoManagedMemory()
    {
        var camera = new Camera2D();
        camera.Follow(Vector2.Zero, Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
        for (var frame = 1; frame <= 64; frame++)
        {
            camera.Follow(new Vector2(frame, frame * 0.25f), Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 65; frame < 4_161; frame++)
        {
            camera.Follow(new Vector2(frame, frame * 0.25f), Viewport, smoothing: 0.2f, deltaSeconds: DeltaSeconds);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
