namespace Game.Core.World.Structures;

public sealed class StructureTemplate
{
    private readonly ushort?[] _tiles;

    public StructureTemplate(int width, int height, IReadOnlyList<ushort?> tiles)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Structure width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Structure height must be greater than zero.");
        }

        if (tiles.Count != width * height)
        {
            throw new ArgumentException("Tile data length must match width * height.", nameof(tiles));
        }

        Width = width;
        Height = height;
        _tiles = tiles.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public ushort? GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Structure coordinate ({x}, {y}) is outside the template.");
        }

        return _tiles[y * Width + x];
    }

    public static StructureTemplate FromRows(IReadOnlyList<string> rows, IReadOnlyDictionary<char, ushort> tileMap, char empty = '.')
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tileMap);

        if (rows.Count == 0)
        {
            throw new ArgumentException("Structure must contain at least one row.", nameof(rows));
        }

        var width = rows[0].Length;
        if (width == 0 || rows.Any(row => row.Length != width))
        {
            throw new ArgumentException("All structure rows must have the same non-zero length.", nameof(rows));
        }

        var tiles = new List<ushort?>(width * rows.Count);
        foreach (var row in rows)
        {
            foreach (var character in row)
            {
                if (character == empty)
                {
                    tiles.Add(null);
                    continue;
                }

                if (!tileMap.TryGetValue(character, out var tileId))
                {
                    throw new KeyNotFoundException($"Structure character '{character}' is not mapped to a tile id.");
                }

                tiles.Add(tileId);
            }
        }

        return new StructureTemplate(width, rows.Count, tiles);
    }
}
