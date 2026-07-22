using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class Camera2D
{
    private const float XDeadzone = 20f;
    private const float YDeadzone = 12f;
    private const float MinimumSmoothTime = 0.055f;
    private const float MaximumSmoothTime = 0.32f;
    private const float TeleportSnapDistance = 640f;
    private const float LookAheadSeconds = 0.10f;
    private const float LookAheadLimit = 24f;
    private const float MaximumLookAheadLimit = 256f;
    private const float LookAheadResponse = 10f;

    private Vector2 _position;
    private Vector2 _previousTarget;
    private float _horizontalLookAhead;
    private bool _hasTrackedTarget;

    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = IsFinite(value) ? value : Vector2.Zero;
            Velocity = Vector2.Zero;
            _horizontalLookAhead = 0f;
            _hasTrackedTarget = false;
        }
    }

    public float HorizontalLookAhead => _horizontalLookAhead;

    public Vector2 Velocity { get; private set; }

    public float Zoom { get; set; } = 2f;

    public Matrix ViewMatrix { get; private set; } = Matrix.Identity;

    public Rectangle VisibleWorldRect { get; private set; }

    public void Follow(
        Vector2 targetWorldPosition,
        Rectangle viewportBounds,
        float smoothing = 1f,
        float deltaSeconds = 1f / 60f,
        float lookAheadLimitPixels = LookAheadLimit)
    {
        if (!IsFinite(targetWorldPosition) || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
        {
            Velocity = Vector2.Zero;
            Recalculate(viewportBounds);
            return;
        }

        smoothing = float.IsFinite(smoothing)
            ? MathHelper.Clamp(smoothing, 0f, 1f)
            : 0f;
        var lookAheadLimit = float.IsFinite(lookAheadLimitPixels)
            ? MathHelper.Clamp(lookAheadLimitPixels, 0f, MaximumLookAheadLimit)
            : LookAheadLimit;
        var frameDelta = MathHelper.Clamp(deltaSeconds, 1f / 240f, 1f / 15f);

        if (!_hasTrackedTarget)
        {
            _previousTarget = targetWorldPosition;
            _hasTrackedTarget = true;
            SnapTo(targetWorldPosition);
            Recalculate(viewportBounds);
            return;
        }

        var targetStep = targetWorldPosition - _previousTarget;
        _previousTarget = targetWorldPosition;
        if (targetStep.LengthSquared() >= TeleportSnapDistance * TeleportSnapDistance)
        {
            SnapTo(targetWorldPosition);
            Recalculate(viewportBounds);
            return;
        }

        var targetVelocityX = targetStep.X / frameDelta;
        var desiredLookAhead = MathF.Abs(targetVelocityX) < 24f
            ? 0f
            : MathHelper.Clamp(targetVelocityX * LookAheadSeconds, -lookAheadLimit, lookAheadLimit);
        var lookAheadBlend = 1f - MathF.Exp(-LookAheadResponse * frameDelta);
        _horizontalLookAhead += (desiredLookAhead - _horizontalLookAhead) * lookAheadBlend;
        _horizontalLookAhead = MathHelper.Clamp(_horizontalLookAhead, -lookAheadLimit, lookAheadLimit);

        if (smoothing >= 0.999f)
        {
            SnapTo(targetWorldPosition);
            Recalculate(viewportBounds);
            return;
        }

        if (smoothing <= 0f)
        {
            Velocity = Vector2.Zero;
            Recalculate(viewportBounds);
            return;
        }

        var trackedTarget = targetWorldPosition + new Vector2(_horizontalLookAhead, 0f);
        var displacement = trackedTarget - _position;
        var deadzoneDisplacement = new Vector2(
            ApplyDeadzone(displacement.X, XDeadzone),
            ApplyDeadzone(displacement.Y, YDeadzone));
        if (deadzoneDisplacement == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            Recalculate(viewportBounds);
            return;
        }

        var desiredPosition = _position + deadzoneDisplacement;
        var smoothTime = MathHelper.Lerp(MaximumSmoothTime, MinimumSmoothTime, smoothing);
        var followVelocity = Velocity;
        _position.X = CriticallyDamped(
            _position.X,
            desiredPosition.X,
            ref followVelocity.X,
            smoothTime,
            frameDelta);
        _position.Y = CriticallyDamped(
            _position.Y,
            desiredPosition.Y,
            ref followVelocity.Y,
            smoothTime,
            frameDelta);
        Velocity = followVelocity;

        Recalculate(viewportBounds);
    }

    private static float ApplyDeadzone(float displacement, float deadzone)
    {
        if (displacement > deadzone)
        {
            return displacement - deadzone;
        }

        if (displacement < -deadzone)
        {
            return displacement + deadzone;
        }

        return 0f;
    }

    private void SnapTo(Vector2 targetWorldPosition)
    {
        _position = targetWorldPosition;
        Velocity = Vector2.Zero;
        _horizontalLookAhead = 0f;
    }

    private static float CriticallyDamped(
        float current,
        float target,
        ref float velocity,
        float smoothTime,
        float deltaSeconds)
    {
        var angularFrequency = 2f / MathF.Max(0.0001f, smoothTime);
        var displacement = current - target;
        var decay = MathF.Exp(-angularFrequency * deltaSeconds);
        var velocityTerm = (velocity + angularFrequency * displacement) * deltaSeconds;
        var result = target + (displacement + velocityTerm) * decay;
        velocity = (velocity - angularFrequency * velocityTerm) * decay;
        return result;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private float ResolveZoom()
    {
        return float.IsFinite(Zoom) ? Math.Max(0.1f, Zoom) : 1f;
    }

    public void Recalculate(Rectangle viewportBounds)
    {
        var zoom = ResolveZoom();
        var halfViewportWorld = new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f / zoom;
        var topLeft = Position - halfViewportWorld;
        var size = halfViewportWorld * 2f;

        VisibleWorldRect = new Rectangle(
            (int)MathF.Floor(topLeft.X),
            (int)MathF.Floor(topLeft.Y),
            (int)MathF.Ceiling(size.X),
            (int)MathF.Ceiling(size.Y));

        ViewMatrix =
            Matrix.CreateTranslation(new Vector3(-Position, 0f)) *
            Matrix.CreateScale(zoom, zoom, 1f) *
            Matrix.CreateTranslation(viewportBounds.Width * 0.5f, viewportBounds.Height * 0.5f, 0f);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition, Rectangle viewportBounds)
    {
        return (worldPosition - Position) * ResolveZoom() + new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, Rectangle viewportBounds)
    {
        return (screenPosition - new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f) / ResolveZoom() + Position;
    }

    public IEnumerable<ChunkPos> GetVisibleChunks()
    {
        GetVisibleChunkBounds(out var minChunk, out var maxChunk);

        for (var cy = minChunk.Y; cy <= maxChunk.Y; cy++)
        {
            for (var cx = minChunk.X; cx <= maxChunk.X; cx++)
            {
                yield return new ChunkPos(cx, cy);
            }
        }
    }

    public void GetVisibleChunkBounds(out ChunkPos minimum, out ChunkPos maximum)
    {
        var minTile = CoordinateUtils.WorldToTile(VisibleWorldRect.Left, VisibleWorldRect.Top);
        var maxTile = CoordinateUtils.WorldToTile(VisibleWorldRect.Right, VisibleWorldRect.Bottom);
        minimum = CoordinateUtils.TileToChunk(minTile);
        maximum = CoordinateUtils.TileToChunk(maxTile);
    }
}
