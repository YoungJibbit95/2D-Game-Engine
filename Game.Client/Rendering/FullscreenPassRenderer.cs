using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class FullscreenPassRenderer
{
    public void Draw(RenderContext context, Texture2D source, Effect? effect = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        context.SpriteBatch.Draw(
            source,
            context.ViewportBounds,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            SpriteEffects.None,
            0f);
    }
}
