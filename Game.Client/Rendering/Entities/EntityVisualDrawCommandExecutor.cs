using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Client.Rendering.Entities;

public sealed class EntityVisualDrawCommandExecutor : IDisposable
{
    private const int ShadowTextureWidth = 24;
    private const int ShadowTextureHeight = 12;

    private Texture2D? _shadowTexture;

    public bool IsPrepared => _shadowTexture is not null;

    public void PrepareResources(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (_shadowTexture is not null && !_shadowTexture.IsDisposed)
        {
            return;
        }

        _shadowTexture?.Dispose();
        _shadowTexture = new Texture2D(graphicsDevice, ShadowTextureWidth, ShadowTextureHeight, false, SurfaceFormat.Color);
        var pixels = new Color[ShadowTextureWidth * ShadowTextureHeight];
        for (var y = 0; y < ShadowTextureHeight; y++)
        {
            for (var x = 0; x < ShadowTextureWidth; x++)
            {
                var nx = (x + 0.5f) / ShadowTextureWidth * 2f - 1f;
                var ny = (y + 0.5f) / ShadowTextureHeight * 2f - 1f;
                var distance = nx * nx + ny * ny;
                var alpha = Math.Clamp(1f - distance, 0f, 1f);
                pixels[y * ShadowTextureWidth + x] = new Color(
                    byte.MaxValue,
                    byte.MaxValue,
                    byte.MaxValue,
                    (byte)(alpha * alpha * 255f));
            }
        }

        _shadowTexture.SetData(pixels);
    }

    public void Draw(
        RenderContext context,
        ClientTextureRegistry textures,
        EntityVisualCommandBuffer commandBuffer,
        Camera2D? camera = null)
    {
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(commandBuffer);
        var zoom = camera?.Zoom ?? 1f;
        var submission = commandBuffer.SubmissionPlan;
        if (submission.Telemetry.IsComplete && submission.Count == commandBuffer.Count)
        {
            for (var index = 0; index < submission.Count; index++)
            {
                DrawCommand(context, textures, commandBuffer[submission[index]], camera, zoom);
            }

            return;
        }

        for (var index = 0; index < commandBuffer.Count; index++)
        {
            DrawCommand(context, textures, commandBuffer[index], camera, zoom);
        }
    }

    public void Dispose()
    {
        _shadowTexture?.Dispose();
        _shadowTexture = null;
    }

    private void DrawCommand(
        RenderContext context,
        ClientTextureRegistry textures,
        in EntityVisualDrawCommand command,
        Camera2D? camera,
        float zoom)
    {
        var position = camera is null
            ? ToXna(command.Position)
            : camera.WorldToScreen(ToXna(command.Position), context.ViewportBounds);
        switch (command.Kind)
        {
            case EntityVisualDrawCommandKind.ShadowEllipse:
                DrawShadow(context.SpriteBatch, context.Pixel, command, position, zoom);
                break;
            case EntityVisualDrawCommandKind.Outline:
                DrawOutline(context.SpriteBatch, textures, command, position, zoom);
                break;
            default:
                DrawSprite(context.SpriteBatch, textures, command, position, zoom);
                break;
        }
    }

    private void DrawShadow(
        SpriteBatch spriteBatch,
        Texture2D fallbackPixel,
        in EntityVisualDrawCommand command,
        Vector2 position,
        float zoom)
    {
        var texture = _shadowTexture ?? fallbackPixel;
        var width = Math.Max(1, texture.Width);
        var height = Math.Max(1, texture.Height);
        spriteBatch.Draw(
            texture,
            position,
            null,
            ToColor(command.Color),
            0f,
            new Vector2(width * 0.5f, height * 0.5f),
            new Vector2(command.Scale.X * zoom / width, command.Scale.Y * zoom / height),
            SpriteEffects.None,
            command.LayerDepth);
    }

    private static void DrawOutline(
        SpriteBatch spriteBatch,
        ClientTextureRegistry textures,
        in EntityVisualDrawCommand command,
        Vector2 position,
        float zoom)
    {
        if (!TryResolveResidentSprite(textures, command.SpriteId, command.FrameIndex, out var sprite))
        {
            return;
        }

        var source = sprite.SourceRectangle;
        var origin = new Vector2(
            source.Width * command.OriginNormalized.X,
            source.Height * command.OriginNormalized.Y);
        var color = ToColor(command.Color);
        var effects = command.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        var scale = ToXna(command.Scale) * zoom;
        var thickness = Math.Max(0.5f, command.Thickness * zoom);
        DrawOutlineOffset(spriteBatch, sprite, source, position, color, command, origin, scale, effects, -thickness, 0f);
        DrawOutlineOffset(spriteBatch, sprite, source, position, color, command, origin, scale, effects, thickness, 0f);
        DrawOutlineOffset(spriteBatch, sprite, source, position, color, command, origin, scale, effects, 0f, -thickness);
        DrawOutlineOffset(spriteBatch, sprite, source, position, color, command, origin, scale, effects, 0f, thickness);
    }

    private static void DrawOutlineOffset(
        SpriteBatch spriteBatch,
        SpriteTexture sprite,
        Rectangle source,
        Vector2 position,
        Color color,
        in EntityVisualDrawCommand command,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float offsetX,
        float offsetY)
    {
        spriteBatch.Draw(
            sprite.Texture,
            position + new Vector2(offsetX, offsetY),
            source,
            color,
            command.RotationRadians,
            origin,
            scale,
            effects,
            command.LayerDepth);
    }

    private static void DrawSprite(
        SpriteBatch spriteBatch,
        ClientTextureRegistry textures,
        in EntityVisualDrawCommand command,
        Vector2 position,
        float zoom)
    {
        if (!TryResolveResidentSprite(textures, command.SpriteId, command.FrameIndex, out var sprite))
        {
            return;
        }

        var source = sprite.SourceRectangle;
        spriteBatch.Draw(
            sprite.Texture,
            position,
            source,
            ToColor(command.Color),
            command.RotationRadians,
            new Vector2(
                source.Width * command.OriginNormalized.X,
                source.Height * command.OriginNormalized.Y),
            ToXna(command.Scale) * zoom,
            command.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            command.LayerDepth);
    }

    private static bool TryResolveResidentSprite(
        ClientTextureRegistry textures,
        string spriteId,
        int frameIndex,
        out SpriteTexture sprite)
    {
        if (!string.IsNullOrWhiteSpace(spriteId) && textures.IsResident(spriteId))
        {
            sprite = textures.Get(spriteId, frameIndex);
            return true;
        }

        if (textures.IsResident(textures.FallbackAssetId))
        {
            sprite = textures.Get(textures.FallbackAssetId);
            return true;
        }

        sprite = null!;
        return false;
    }

    private static Vector2 ToXna(NumericsVector2 value)
    {
        return new Vector2(value.X, value.Y);
    }

    private static Color ToColor(EntityVisualColor value)
    {
        return new Color(value.R, value.G, value.B, value.A);
    }
}
