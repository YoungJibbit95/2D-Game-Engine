using Game.Core.Equipment;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public enum MobilityAbilityKind
{
    DoubleJump,
    Flight,
    Glide
}

public readonly record struct MobilityAbilityPresentation(
    MobilityAbilityKind Kind,
    string SpriteId,
    int FrameIndex);

public readonly record struct PixelMobilityDockLayout(
    Rectangle Dock,
    int SlotSize,
    int SlotGap,
    int Count)
{
    public Rectangle Slot(int index)
    {
        if (index < 0 || index >= Count || Dock.IsEmpty)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(
            Dock.X + 6 + index * (SlotSize + SlotGap),
            Dock.Y + 6,
            SlotSize,
            SlotSize);
    }
}

public static class MobilityAbilityDockPlanner
{
    public const int MaximumAbilityCount = 3;
    public const string SpriteId = "ui/mobility_abilities";

    public static int Build(
        in PlayerStatBlock stats,
        Span<MobilityAbilityPresentation> destination)
    {
        var count = 0;
        if (stats.CanDoubleJump)
        {
            Add(destination, ref count, MobilityAbilityKind.DoubleJump, frameIndex: 0);
        }

        if (stats.CanFly)
        {
            Add(destination, ref count, MobilityAbilityKind.Flight, frameIndex: 1);
        }

        if (stats.CanGlide)
        {
            Add(destination, ref count, MobilityAbilityKind.Glide, frameIndex: 2);
        }

        return count;
    }

    public static PixelMobilityDockLayout ResolveLayout(
        Rectangle viewport,
        Rectangle statusDock,
        PixelUiDensity density,
        int abilityCount)
    {
        var count = Math.Clamp(abilityCount, 0, MaximumAbilityCount);
        if (count == 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return new PixelMobilityDockLayout(Rectangle.Empty, 0, 0, 0);
        }

        var edge = density switch
        {
            PixelUiDensity.Compact => 8,
            PixelUiDensity.Expanded => 24,
            _ => 16
        };
        var preferredSlotSize = density switch
        {
            PixelUiDensity.Compact => 24,
            PixelUiDensity.Expanded => 34,
            _ => 30
        };
        var gap = density == PixelUiDensity.Compact ? 3 : 4;
        var right = statusDock.IsEmpty
            ? viewport.Right - edge
            : statusDock.X - 6;
        var leftLimit = viewport.X + edge;
        var availableWidth = Math.Max(0, right - leftLimit);
        var chromeWidth = 12 + Math.Max(0, count - 1) * gap;
        var slotSize = Math.Min(
            preferredSlotSize,
            Math.Max(0, (availableWidth - chromeWidth) / count));
        if (slotSize <= 0)
        {
            return new PixelMobilityDockLayout(Rectangle.Empty, 0, 0, 0);
        }

        var width = count * slotSize + Math.Max(0, count - 1) * gap + 12;
        var height = Math.Min(viewport.Height, slotSize + 12);
        var desiredY = statusDock.IsEmpty
            ? viewport.Y + edge + 120
            : statusDock.Y;
        var y = Math.Clamp(
            desiredY,
            viewport.Y,
            Math.Max(viewport.Y, viewport.Bottom - edge - height));
        var x = Math.Max(viewport.X, right - width);
        return new PixelMobilityDockLayout(
            new Rectangle(x, y, Math.Min(width, viewport.Right - x), height),
            slotSize,
            gap,
            count);
    }

    private static void Add(
        Span<MobilityAbilityPresentation> destination,
        ref int count,
        MobilityAbilityKind kind,
        int frameIndex)
    {
        if ((uint)count >= (uint)destination.Length)
        {
            return;
        }

        destination[count++] = new MobilityAbilityPresentation(kind, SpriteId, frameIndex);
    }
}
