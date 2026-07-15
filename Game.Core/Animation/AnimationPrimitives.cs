using System.Numerics;

namespace Game.Core.Animation;

public enum AnimationLoopMode
{
    Loop,
    PingPong,
    Once
}

public enum CharacterFacingDirection
{
    Left = -1,
    Right = 1
}

public readonly record struct AnimationColor(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    public static AnimationColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public AnimationColor Multiply(AnimationColor other)
    {
        return new AnimationColor(
            MultiplyChannel(R, other.R),
            MultiplyChannel(G, other.G),
            MultiplyChannel(B, other.B),
            MultiplyChannel(A, other.A));
    }

    public AnimationColor WithOpacity(float opacity)
    {
        var clamped = Math.Clamp(opacity, 0f, 1f);
        return this with { A = (byte)MathF.Round(A * clamped) };
    }

    private static byte MultiplyChannel(byte left, byte right)
    {
        return (byte)((left * right + 127) / 255);
    }
}

public readonly record struct AnimationTransform2D(
    Vector2 Translation,
    float RotationRadians,
    Vector2 Scale)
{
    public static AnimationTransform2D Identity { get; } = new(Vector2.Zero, 0f, Vector2.One);
}

public readonly record struct AttachmentSocketPose(
    string Id,
    Vector2 Position,
    float RotationRadians = 0f);

public readonly record struct AnimationPlaybackRate
{
    public AnimationPlaybackRate(int numerator, int denominator)
    {
        if (numerator < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "Playback-rate numerator cannot be negative.");
        }

        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "Playback-rate denominator must be positive.");
        }

        var divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public int Numerator { get; }

    public int Denominator { get; }

    public static AnimationPlaybackRate Normal { get; } = new(1, 1);

    public static AnimationPlaybackRate FromPercentage(int percentage)
    {
        return new AnimationPlaybackRate(Math.Max(0, percentage), 100);
    }

    public static AnimationPlaybackRate FromLocomotionSpeed(
        int speedMilliUnitsPerSecond,
        int referenceSpeedMilliUnitsPerSecond,
        int minimumPercentage = 50,
        int maximumPercentage = 200)
    {
        if (referenceSpeedMilliUnitsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referenceSpeedMilliUnitsPerSecond),
                "Reference speed must be positive.");
        }

        if (minimumPercentage < 0 || maximumPercentage < minimumPercentage)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPercentage),
                "Playback percentage range is invalid.");
        }

        var absoluteSpeed = Math.Abs((long)speedMilliUnitsPerSecond);
        var percentage = (int)Math.Clamp(
            absoluteSpeed * 100L / referenceSpeedMilliUnitsPerSecond,
            minimumPercentage,
            maximumPercentage);

        return FromPercentage(percentage);
    }

    public AnimationPlaybackRate Multiply(AnimationPlaybackRate other)
    {
        var numerator = checked((long)Numerator * other.Numerator);
        var denominator = checked((long)Denominator * other.Denominator);
        if (numerator > int.MaxValue || denominator > int.MaxValue)
        {
            throw new OverflowException("Combined animation playback rate is too large.");
        }

        return new AnimationPlaybackRate((int)numerator, (int)denominator);
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return Math.Max(1, left);
    }
}
