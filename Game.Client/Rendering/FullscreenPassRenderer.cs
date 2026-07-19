using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class FullscreenPassRenderer
{
    public void Draw(
        RenderContext context,
        Texture2D source,
        Effect? effect = null,
        BlendState? blendState = null,
        SamplerState? samplerState = null,
        Color? tint = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (effect is null)
        {
            context.SpriteBatch.Draw(
                source,
                context.ViewportBounds,
                null,
                tint ?? Color.White,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f);
            return;
        }

        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            SpriteSortMode.Immediate,
            blendState ?? BlendState.Opaque,
            samplerState ?? SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            effect);
        context.SpriteBatch.Draw(
            source,
            context.ViewportBounds,
            null,
            tint ?? Color.White,
            0f,
            Vector2.Zero,
            SpriteEffects.None,
            0f);
        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
    }
}
