using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Tiny built-in 5x7 pixel font so HUD rendering can work without SpriteFont/content pipeline.
/// This is a pragmatic placeholder until real NES font assets are added.
/// </summary>
internal sealed class NesHudFont
{
    // Each glyph is 7 rows, 5 bits per row (MSB on the left).
    private readonly Dictionary<char, byte[]> _glyphs = new()
    {
        // Digits
        ['0'] = Rows(0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110),
        ['1'] = Rows(0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110),
        ['2'] = Rows(0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111),
        ['3'] = Rows(0b01110, 0b10001, 0b00001, 0b00110, 0b00001, 0b10001, 0b01110),
        ['4'] = Rows(0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010),
        ['5'] = Rows(0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110),
        ['6'] = Rows(0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110),
        ['7'] = Rows(0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000),
        ['8'] = Rows(0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110),
        ['9'] = Rows(0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100),

        // Letters (subset but we include A-Z for scoreboard + ball spot strings)
        ['A'] = Rows(0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001),
        ['B'] = Rows(0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110),
        ['C'] = Rows(0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110),
        ['D'] = Rows(0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110),
        ['E'] = Rows(0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111),
        ['F'] = Rows(0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000),
        ['G'] = Rows(0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110),
        ['H'] = Rows(0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001),
        ['I'] = Rows(0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110),
        ['J'] = Rows(0b00001, 0b00001, 0b00001, 0b00001, 0b10001, 0b10001, 0b01110),
        ['K'] = Rows(0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001),
        ['L'] = Rows(0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111),
        ['M'] = Rows(0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001),
        ['N'] = Rows(0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001),
        ['O'] = Rows(0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110),
        ['P'] = Rows(0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000),
        ['Q'] = Rows(0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101),
        ['R'] = Rows(0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001),
        ['S'] = Rows(0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110),
        ['T'] = Rows(0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100),
        ['U'] = Rows(0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110),
        ['V'] = Rows(0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100),
        ['W'] = Rows(0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001),
        ['X'] = Rows(0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001),
        ['Y'] = Rows(0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100),
        ['Z'] = Rows(0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111),

        // Punctuation / misc
        [':'] = Rows(0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b00000),
        ['-'] = Rows(0b00000, 0b00000, 0b00000, 0b01110, 0b00000, 0b00000, 0b00000),
        ['&'] = Rows(0b01100, 0b10010, 0b10100, 0b01000, 0b10101, 0b10010, 0b01101),
        [' '] = Rows(0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000),
    };

    private static byte[] Rows(int r0, int r1, int r2, int r3, int r4, int r5, int r6)
        => new[] { (byte)r0, (byte)r1, (byte)r2, (byte)r3, (byte)r4, (byte)r5, (byte)r6 };

    public Point Measure(string text, int scale = 1, int spacing = 1)
    {
        if (string.IsNullOrEmpty(text))
            return Point.Zero;

        var w = (text.Length * (5 * scale)) + ((text.Length - 1) * spacing);
        var h = 7 * scale;
        return new Point(w, h);
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, string text, Vector2 pos, Color color, int scale = 1, int spacing = 1)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var x = (int)pos.X;
        var y = (int)pos.Y;

        foreach (var rawCh in text)
        {
            var ch = char.ToUpperInvariant(rawCh);
            if (!_glyphs.TryGetValue(ch, out var rows))
                rows = _glyphs[' '];

            for (var row = 0; row < 7; row++)
            {
                var bits = rows[row];
                for (var col = 0; col < 5; col++)
                {
                    // bit 4 is leftmost
                    var on = ((bits >> (4 - col)) & 1) != 0;
                    if (!on)
                        continue;

                    sb.Draw(pixel, new Rectangle(x + (col * scale), y + (row * scale), scale, scale), color);
                }
            }

            x += (5 * scale) + spacing;
        }
    }
}
