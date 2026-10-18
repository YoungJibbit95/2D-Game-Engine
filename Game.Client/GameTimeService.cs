using Microsoft.Xna.Framework;

namespace Game.Client;

public sealed class GameTimeService
{
    private double _fpsWindowSeconds;
    private int _drawFramesInWindow;

    public double FrameDeltaSeconds { get; private set; }

    public double TotalSeconds { get; private set; }

    public double FramesPerSecond { get; private set; }

    public void BeginFrame(GameTime gameTime)
    {
        BeginFrame(gameTime.ElapsedGameTime.TotalSeconds, gameTime.TotalGameTime.TotalSeconds);
    }

    public void BeginFrame(double deltaSeconds, double totalSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 || !double.IsFinite(totalSeconds) || totalSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }
        FrameDeltaSeconds = deltaSeconds;
        TotalSeconds = totalSeconds;
        _fpsWindowSeconds += FrameDeltaSeconds;
    }

    public void RecordDrawFrame()
    {
        _drawFramesInWindow++;

        if (_fpsWindowSeconds < 0.25)
        {
            return;
        }

        FramesPerSecond = _drawFramesInWindow / _fpsWindowSeconds;
        _drawFramesInWindow = 0;
        _fpsWindowSeconds = 0;
    }
}
