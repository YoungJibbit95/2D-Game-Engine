using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;

namespace Game.Client.UI;

public static class ItemIconRenderer
{
    private const int CachedCountLimit = 1000;
    private static readonly ConditionalWeakTable<ItemDefinition, FallbackLabel> FallbackLabels = new();
    private static readonly string?[] CountLabels = new string[CachedCountLimit];
    private static readonly Dictionary<int, string> LargeCountLabels = new();

    public static void DrawItemStack(
        RenderContext context,
        ClientTextureRegistry? textures,
        IItemDefinitionProvider items,
        ItemStack stack,
        Rectangle bounds,
        UiPalette palette,
        bool drawCount = true,
        float opacity = 1f,
        bool drawRarityFrame = true,
        bool isFavorite = false)
    {
        if (stack.IsEmpty)
        {
            return;
        }

        var item = items.GetById(stack.ItemId);
        if (drawRarityFrame)
        {
            DrawRarityFrame(context, bounds, item.Rarity, opacity);
        }

        var iconBounds = new Rectangle(bounds.X + 7, bounds.Y + 6, bounds.Width - 14, bounds.Height - 14);
        if (!TryDrawSprite(context, textures, item.TexturePath, iconBounds, opacity))
        {
            context.DebugText.Draw(new Vector2(bounds.X + 5, bounds.Y + 6), GetFallbackLabel(item), palette.Text, 1);
        }

        if (drawCount && stack.Count > 1)
        {
            var count = GetCountLabel(stack.Count);
            var countX = bounds.Right - Math.Min(bounds.Width - 8, count.Length * 7 + 8);
            var countY = bounds.Bottom - 14;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(countX - 2, countY - 1, count.Length * 7 + 5, 12), UiTheme.WithAlpha(palette.Backdrop, 0.64f * opacity));
            context.DebugText.Draw(new Vector2(countX, countY), count, palette.Warning, 1);
        }

        if (isFavorite)
        {
            context.DebugText.Draw(new Vector2(bounds.Right - 12, bounds.Y + 3), "*", palette.Warning, 1);
        }
    }

    public static void DrawRarityFrame(RenderContext context, Rectangle bounds, ItemRarity rarity, float opacity = 1f)
    {
        var color = GetRarityColor(rarity);
        var thickness = rarity == ItemRarity.Common ? 1 : 2;
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(color, 0.9f * opacity), thickness);
    }

    public static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Uncommon => new Color(111, 210, 124),
            ItemRarity.Rare => new Color(91, 164, 244),
            ItemRarity.Epic => new Color(194, 112, 232),
            ItemRarity.Legendary => new Color(245, 178, 67),
            ItemRarity.Quest => new Color(239, 104, 105),
            _ => new Color(145, 157, 171)
        };
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

    internal static string GetFallbackLabel(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return FallbackLabels.GetValue(item, static definition => new FallbackLabel(Abbreviate(definition.DisplayName))).Value;
    }

    internal static string GetCountLabel(int count)
    {
        if ((uint)count < CachedCountLimit)
        {
            var cached = Volatile.Read(ref CountLabels[count]);
            if (cached is not null)
            {
                return cached;
            }

            var created = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Interlocked.CompareExchange(ref CountLabels[count], created, null) ?? created;
        }

        lock (LargeCountLabels)
        {
            if (!LargeCountLabels.TryGetValue(count, out var cached))
            {
                cached = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LargeCountLabels.Add(count, cached);
            }

            return cached;
        }
    }

    internal static string Abbreviate(string text)
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

    private sealed record FallbackLabel(string Value);
}
