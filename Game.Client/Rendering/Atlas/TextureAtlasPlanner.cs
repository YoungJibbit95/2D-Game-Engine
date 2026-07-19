using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Atlas;

public readonly record struct TextureAtlasSourceSize(int Width, int Height);

public readonly record struct TextureAtlasPlacement(int PageIndex, Rectangle ContentBounds)
{
    public bool IsPlaced => PageIndex >= 0 && !ContentBounds.IsEmpty;

    public static TextureAtlasPlacement Unplaced { get; } = new(-1, Rectangle.Empty);
}

public readonly record struct TextureAtlasPlan(
    int PageCount,
    int PlacedSourceCount,
    int UnplacedSourceCount,
    int PageWidth,
    int PageHeight,
    int Padding,
    long EstimatedPageBytes);

public static class TextureAtlasPlanner
{
    public static TextureAtlasPlan Build(
        ReadOnlySpan<TextureAtlasSourceSize> sources,
        int pageWidth,
        int pageHeight,
        int padding,
        Span<TextureAtlasPlacement> placements)
    {
        if (pageWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageWidth));
        }

        if (pageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageHeight));
        }

        if (padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(padding));
        }

        if (placements.Length < sources.Length)
        {
            throw new ArgumentException("Placement storage is smaller than the source list.", nameof(placements));
        }

        var page = 0;
        var pageHasContent = false;
        var x = 0;
        var y = 0;
        var rowHeight = 0;
        var placed = 0;
        var unplaced = 0;
        for (var index = 0; index < sources.Length; index++)
        {
            ref readonly var source = ref sources[index];
            var paddedWidth = (long)source.Width + padding * 2L;
            var paddedHeight = (long)source.Height + padding * 2L;
            if (source.Width <= 0 || source.Height <= 0 ||
                paddedWidth > pageWidth || paddedHeight > pageHeight)
            {
                placements[index] = TextureAtlasPlacement.Unplaced;
                unplaced++;
                continue;
            }

            if ((long)x + paddedWidth > pageWidth)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            if ((long)y + paddedHeight > pageHeight)
            {
                page++;
                pageHasContent = false;
                x = 0;
                y = 0;
                rowHeight = 0;
            }

            placements[index] = new TextureAtlasPlacement(
                page,
                new Rectangle(x + padding, y + padding, source.Width, source.Height));
            x += checked((int)paddedWidth);
            rowHeight = Math.Max(rowHeight, checked((int)paddedHeight));
            pageHasContent = true;
            placed++;
        }

        var pageCount = placed == 0 ? 0 : page + (pageHasContent ? 1 : 0);
        var pageBytes = SaturatingMultiply(SaturatingMultiply(pageWidth, pageHeight), 4L);
        return new TextureAtlasPlan(
            pageCount,
            placed,
            unplaced,
            pageWidth,
            pageHeight,
            padding,
            SaturatingMultiply(pageBytes, pageCount));
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }
}
