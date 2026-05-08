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
        FrameDeltaSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        TotalSeconds = gameTime.TotalGameTime.TotalSeconds;
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
