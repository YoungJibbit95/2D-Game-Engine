using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class DebugTextRenderer
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int GlyphSpacing = 1;

    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        [' '] = new[]
        {
            "     ",
            "     ",
            "     ",
            "     ",
            "     ",
            "     ",
            "     "
        },
        ['.'] = new[]
        {
            "     ",
            "     ",
            "     ",
            "     ",
            "     ",
            " XX  ",
            " XX  "
        },
        [':'] = new[]
        {
            "     ",
            " XX  ",
            " XX  ",
            "     ",
            " XX  ",
            " XX  ",
            "     "
        },
        ['0'] = new[]
        {
            " XXX ",
            "X   X",
            "X  XX",
            "X X X",
            "XX  X",
            "X   X",
            " XXX "
        },
        ['1'] = new[]
        {
            "  X  ",
            " XX  ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  ",
            " XXX "
        },
        ['2'] = new[]
        {
            " XXX ",
            "X   X",
            "    X",
            "   X ",
            "  X  ",
            " X   ",
            "XXXXX"
        },
        ['3'] = new[]
        {
            " XXX ",
            "X   X",
            "    X",
            "  XX ",
            "    X",
            "X   X",
            " XXX "
        },
        ['4'] = new[]
        {
            "   X ",
            "  XX ",
            " X X ",
            "X  X ",
            "XXXXX",
            "   X ",
            "   X "
        },
        ['5'] = new[]
        {
            "XXXXX",
            "X    ",
            "X    ",
            "XXXX ",
            "    X",
            "X   X",
            " XXX "
        },
        ['6'] = new[]
        {
            " XXX ",
            "X   X",
            "X    ",
            "XXXX ",
            "X   X",
            "X   X",
            " XXX "
        },
        ['7'] = new[]
        {
            "XXXXX",
            "    X",
            "   X ",
            "  X  ",
            " X   ",
            " X   ",
            " X   "
        },
        ['8'] = new[]
        {
            " XXX ",
            "X   X",
            "X   X",
            " XXX ",
            "X   X",
            "X   X",
            " XXX "
        },
        ['9'] = new[]
        {
            " XXX ",
            "X   X",
            "X   X",
            " XXXX",
            "    X",
            "X   X",
            " XXX "
        },
        ['A'] = new[]
        {
            " XXX ",
            "X   X",
            "X   X",
            "XXXXX",
            "X   X",
            "X   X",
            "X   X"
        },
        ['B'] = new[]
        {
            "XXXX ",
            "X   X",
            "X   X",
            "XXXX ",
            "X   X",
            "X   X",
            "XXXX "
        },
        ['C'] = new[]
        {
            " XXX ",
            "X   X",
            "X    ",
            "X    ",
            "X    ",
            "X   X",
            " XXX "
        },
        ['D'] = new[]
        {
            "XXXX ",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "XXXX "
        },
        ['E'] = new[]
        {
            "XXXXX",
            "X    ",
            "X    ",
            "XXXX ",
            "X    ",
            "X    ",
            "XXXXX"
        },
        ['F'] = new[]
        {
            "XXXXX",
            "X    ",
            "X    ",
            "XXXX ",
            "X    ",
            "X    ",
            "X    "
        },
        ['G'] = new[]
        {
            " XXX ",
            "X   X",
            "X    ",
            "X XXX",
            "X   X",
            "X   X",
            " XXX "
        },
        ['H'] = new[]
        {
            "X   X",
            "X   X",
            "X   X",
            "XXXXX",
            "X   X",
            "X   X",
            "X   X"
        },
        ['I'] = new[]
        {
            " XXX ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  ",
            " XXX "
        },
        ['J'] = new[]
        {
            "  XXX",
            "   X ",
            "   X ",
            "   X ",
            "   X ",
            "X  X ",
            " XX  "
        },
        ['K'] = new[]
        {
            "X   X",
            "X  X ",
            "X X  ",
            "XX   ",
            "X X  ",
            "X  X ",
            "X   X"
        },
        ['L'] = new[]
        {
            "X    ",
            "X    ",
            "X    ",
            "X    ",
            "X    ",
            "X    ",
            "XXXXX"
        },
        ['M'] = new[]
        {
            "X   X",
            "XX XX",
            "X X X",
            "X   X",
            "X   X",
            "X   X",
            "X   X"
        },
        ['N'] = new[]
        {
            "X   X",
            "XX  X",
            "X X X",
            "X  XX",
            "X   X",
            "X   X",
            "X   X"
        },
        ['O'] = new[]
        {
            " XXX ",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            " XXX "
        },
        ['P'] = new[]
        {
            "XXXX ",
            "X   X",
            "X   X",
            "XXXX ",
            "X    ",
            "X    ",
            "X    "
        },
        ['R'] = new[]
        {
            "XXXX ",
            "X   X",
            "X   X",
            "XXXX ",
            "X X  ",
            "X  X ",
            "X   X"
        },
        ['Q'] = new[]
        {
            " XXX ",
            "X   X",
            "X   X",
            "X   X",
            "X X X",
            "X  X ",
            " XX X"
        },
        ['S'] = new[]
        {
            " XXXX",
            "X    ",
            "X    ",
            " XXX ",
            "    X",
            "    X",
            "XXXX "
        },
        ['T'] = new[]
        {
            "XXXXX",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  "
        },
        ['U'] = new[]
        {
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            " XXX "
        },
        ['V'] = new[]
        {
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            " X X ",
            "  X  "
        },
        ['W'] = new[]
        {
            "X   X",
            "X   X",
            "X   X",
            "X   X",
            "X X X",
            "XX XX",
            "X   X"
        },
        ['X'] = new[]
        {
            "X   X",
            "X   X",
            " X X ",
            "  X  ",
            " X X ",
            "X   X",
            "X   X"
        },
        ['Y'] = new[]
        {
            "X   X",
            "X   X",
            " X X ",
            "  X  ",
            "  X  ",
            "  X  ",
            "  X  "
        },
        ['Z'] = new[]
        {
            "XXXXX",
            "    X",
            "   X ",
            "  X  ",
            " X   ",
            "X    ",
            "XXXXX"
        }
    };

    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    public DebugTextRenderer(SpriteBatch spriteBatch, Texture2D pixel)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
    }

    public void Draw(Vector2 position, string text, Color color, int scale = 1)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than zero.");
        }

        var cursorX = (int)position.X;
        var cursorY = (int)position.Y;

        foreach (var rawCharacter in text)
        {
            var character = char.ToUpperInvariant(rawCharacter);
            if (!Glyphs.TryGetValue(character, out var glyph))
            {
                cursorX += (GlyphWidth + GlyphSpacing) * scale;
                continue;
            }

            DrawGlyph(cursorX, cursorY, glyph, color, scale);
            cursorX += (GlyphWidth + GlyphSpacing) * scale;
        }
    }

    private void DrawGlyph(int x, int y, string[] glyph, Color color, int scale)
    {
        for (var row = 0; row < GlyphHeight; row++)
        {
            var pattern = glyph[row];
            for (var column = 0; column < GlyphWidth; column++)
            {
                if (pattern[column] == ' ')
                {
                    continue;
                }

                var destination = new Rectangle(
                    x + column * scale,
                    y + row * scale,
                    scale,
                    scale);

                _spriteBatch.Draw(_pixel, destination, color);
            }
        }
    }
}
