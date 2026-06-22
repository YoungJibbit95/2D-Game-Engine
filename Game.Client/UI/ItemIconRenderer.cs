using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public static class ItemIconRenderer
{
    public static void DrawItemStack(
        RenderContext context,
        ClientTextureRegistry? textures,
        IItemDefinitionProvider items,
        ItemStack stack,
        Rectangle bounds,
        UiPalette palette,
        bool drawCount = true,
        float opacity = 1f)
    {
        if (stack.IsEmpty)
        {
            return;
        }

        var item = items.GetById(stack.ItemId);
        var iconBounds = new Rectangle(bounds.X + 7, bounds.Y + 6, bounds.Width - 14, bounds.Height - 14);
        if (!TryDrawSprite(context, textures, item.TexturePath, iconBounds, opacity))
        {
            context.DebugText.Draw(new Vector2(bounds.X + 5, bounds.Y + 6), Abbreviate(item.DisplayName), palette.Text, 1);
        }

        if (drawCount && stack.Count > 1)
        {
            var count = stack.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var countX = bounds.Right - Math.Min(bounds.Width - 8, count.Length * 7 + 8);
            var countY = bounds.Bottom - 14;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(countX - 2, countY - 1, count.Length * 7 + 5, 12), UiTheme.WithAlpha(palette.Backdrop, 0.64f * opacity));
            context.DebugText.Draw(new Vector2(countX, countY), count, palette.Warning, 1);
        }
    }

    public static bool TryDrawSprite(
        RenderContext context,
        ClientTextureRegistry? textures,
        string spriteId,
        Rectangle bounds,
        float opacity = 1f,
        int frameIndex = 0)
    {
        if (textures is null || string.IsNullOrWhiteSpace(spriteId) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var sprite = textures.Get(spriteId, frameIndex);
        if (sprite.IsPlaceholder || sprite.SourceRectangle.Width <= 0 || sprite.SourceRectangle.Height <= 0)
        {
            return false;
        }

        var destination = FitInside(sprite.SourceRectangle, bounds);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X + 2, destination.Y + 2, destination.Width, destination.Height), UiTheme.WithAlpha(Color.Black, 0.22f * opacity));
        context.SpriteBatch.Draw(sprite.Texture, destination, sprite.SourceRectangle, UiTheme.WithAlpha(Color.White, opacity));
        return true;
    }

    private static Rectangle FitInside(Rectangle source, Rectangle bounds)
    {
        var scale = MathF.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        var width = Math.Max(1, (int)MathF.Round(source.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(source.Height * scale));
        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
    }

    private static string Abbreviate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 1)
        {
            return string.Concat(words.Select(word => char.ToUpperInvariant(word[0]))).Substring(0, Math.Min(4, words.Length));
        }

        return text.Length <= 4
            ? text.ToUpperInvariant()
            : text[..4].ToUpperInvariant();
    }
}
