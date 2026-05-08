using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public readonly record struct RenderContext(
    GraphicsDevice GraphicsDevice,
    SpriteBatch SpriteBatch,
    DebugTextRenderer DebugText,
    Texture2D Pixel,
    GameTimeService Time,
    Rectangle ViewportBounds);
