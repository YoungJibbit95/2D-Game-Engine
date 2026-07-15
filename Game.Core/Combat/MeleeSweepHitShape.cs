using Game.Core.World;
using System.Numerics;

namespace Game.Core.Combat;

public sealed record MeleeSweepDefinition
{
    public float Reach { get; init; } = 38;

    public float Radius { get; init; } = 8;

    public float StartAngleRadians { get; init; } = -MathF.PI * 0.6f;

    public float EndAngleRadians { get; init; } = MathF.PI * 0.6f;

    public void Validate()
    {
        if (!float.IsFinite(Reach) || Reach <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Reach));
        }

        if (!float.IsFinite(Radius) || Radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Radius));
        }

        if (!float.IsFinite(StartAngleRadians) || !float.IsFinite(EndAngleRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(StartAngleRadians));
        }
    }
}

public readonly record struct MeleeSweepHitShape(
    Vector2 PreviousTip,
    Vector2 CurrentTip,
    float Radius,
    RectI Bounds)
{
    public bool Intersects(RectI target)
    {
        if (target.IsEmpty || !Bounds.Intersects(target.Inflate((int)MathF.Ceiling(Radius))))
        {
            return false;
        }

        if (SegmentIntersectsRectangle(PreviousTip, CurrentTip, target))
        {
            return true;
        }

        var radiusSquared = Radius * Radius;
        if (DistanceSquaredToRectangle(PreviousTip, target) <= radiusSquared ||
            DistanceSquaredToRectangle(CurrentTip, target) <= radiusSquared)
        {
            return true;
        }

        return DistanceSquaredToSegment(new Vector2(target.Left, target.Top), PreviousTip, CurrentTip) <= radiusSquared ||
               DistanceSquaredToSegment(new Vector2(target.Right, target.Top), PreviousTip, CurrentTip) <= radiusSquared ||
               DistanceSquaredToSegment(new Vector2(target.Left, target.Bottom), PreviousTip, CurrentTip) <= radiusSquared ||
               DistanceSquaredToSegment(new Vector2(target.Right, target.Bottom), PreviousTip, CurrentTip) <= radiusSquared;
    }

    private static bool SegmentIntersectsRectangle(Vector2 start, Vector2 end, RectI target)
    {
        var direction = end - start;
        var minimum = 0f;
        var maximum = 1f;
        return Clip(-direction.X, start.X - target.Left, ref minimum, ref maximum) &&
               Clip(direction.X, target.Right - start.X, ref minimum, ref maximum) &&
               Clip(-direction.Y, start.Y - target.Top, ref minimum, ref maximum) &&
               Clip(direction.Y, target.Bottom - start.Y, ref minimum, ref maximum);
    }

    private static bool Clip(float denominator, float numerator, ref float minimum, ref float maximum)
    {
        if (MathF.Abs(denominator) <= float.Epsilon)
        {
            return numerator >= 0;
        }

        var ratio = numerator / denominator;
        if (denominator < 0)
        {
            if (ratio > maximum)
            {
                return false;
            }

            minimum = Math.Max(minimum, ratio);
        }
        else
        {
            if (ratio < minimum)
            {
                return false;
            }

            maximum = Math.Min(maximum, ratio);
        }

        return true;
    }

    private static float DistanceSquaredToRectangle(Vector2 point, RectI target)
    {
        var closest = new Vector2(
            Math.Clamp(point.X, target.Left, target.Right),
            Math.Clamp(point.Y, target.Top, target.Bottom));
        return Vector2.DistanceSquared(point, closest);
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector2.DistanceSquared(point, start);
        }

        var amount = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0, 1);
        return Vector2.DistanceSquared(point, start + segment * amount);
    }
}

public sealed class MeleeSweepResolver
{
    public MeleeSweepHitShape Resolve(
        Vector2 origin,
        Vector2 facing,
        MeleeSweepDefinition definition,
        float previousProgress,
        float currentProgress)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        ValidateFinite(origin, nameof(origin));
        ValidateFinite(facing, nameof(facing));
        if (facing.LengthSquared() <= float.Epsilon)
        {
            facing = Vector2.UnitX;
        }

        ValidateProgress(previousProgress, nameof(previousProgress));
        ValidateProgress(currentProgress, nameof(currentProgress));
        var baseAngle = MathF.Atan2(facing.Y, facing.X);
        var previousTip = ResolveTip(origin, baseAngle, definition, previousProgress);
        var currentTip = ResolveTip(origin, baseAngle, definition, currentProgress);
        return new MeleeSweepHitShape(
            previousTip,
            currentTip,
            definition.Radius,
            CreateBounds(previousTip, currentTip, definition.Radius));
    }

    private static Vector2 ResolveTip(
        Vector2 origin,
        float baseAngle,
        MeleeSweepDefinition definition,
        float progress)
    {
        var sweepAngle = definition.StartAngleRadians +
            (definition.EndAngleRadians - definition.StartAngleRadians) * progress;
        var angle = baseAngle + sweepAngle;
        return origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * definition.Reach;
    }

    private static RectI CreateBounds(Vector2 start, Vector2 end, float radius)
    {
        var left = SaturateToInt(MathF.Floor(Math.Min(start.X, end.X) - radius));
        var top = SaturateToInt(MathF.Floor(Math.Min(start.Y, end.Y) - radius));
        var right = SaturateToInt(MathF.Ceiling(Math.Max(start.X, end.X) + radius));
        var bottom = SaturateToInt(MathF.Ceiling(Math.Max(start.Y, end.Y) + radius));
        return new RectI(
            left,
            top,
            SaturateLength((long)right - left),
            SaturateLength((long)bottom - top));
    }

    private static int SaturateToInt(float value)
    {
        return value <= int.MinValue ? int.MinValue : value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private static int SaturateLength(long value)
    {
        return (int)Math.Clamp(value, 0, int.MaxValue);
    }

    private static void ValidateFinite(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateProgress(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
