using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Components.PlayCall;

namespace TecmoSBGame.Rendering.PlayCall;

/// <summary>
/// Simplified play diagram preview.
///
/// This is a lightweight, schematic rendering (not the full bank5/6 route scripts).
/// It updates with the currently selected play and displays representative routes/blocking.
/// </summary>
public sealed class PlayDiagramRenderer
{
    private readonly PlayCallUiAssets _assets;

    public PlayDiagramRenderer(PlayCallUiAssets assets)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public void Draw(SpriteBatch sb, Rectangle area, PlayCallComponent pc)
    {
        if (!pc.Visible)
            return;

        var bg = new Color(24, 40, 120);
        sb.Draw(_assets.Pixel, area, bg);

        var headerRect = new Rectangle(area.X, area.Y, area.Width, 22);
        sb.Draw(_assets.Pixel, headerRect, new Color(16, 28, 96));
        sb.DrawString(_assets.Font, "DIAGRAM", new Vector2(area.X + 6, area.Y + 3), Color.White);

        var field = new Rectangle(area.X + 8, area.Y + 30, area.Width - 16, area.Height - 40);
        sb.Draw(_assets.Pixel, field, new Color(0, 96, 0));
        sb.Draw(_assets.Pixel, new Rectangle(field.X, field.Center.Y, field.Width, 1), Color.White);

        var off = pc.SelectedPlay;
        var name = off is null ? "(no play)" : (off.Name ?? "");
        sb.DrawString(_assets.Font, Truncate(name, 20), new Vector2(area.X + 6, area.Bottom - 18), Color.White);

        // Player markers (static schematic positions).
        var qb = new Vector2(field.X + field.Width * 0.30f, field.Center.Y);
        var rb = new Vector2(field.X + field.Width * 0.25f, field.Center.Y + 16);
        var wr1 = new Vector2(field.X + field.Width * 0.20f, field.Center.Y - 36);
        var wr2 = new Vector2(field.X + field.Width * 0.20f, field.Center.Y + 36);
        var te = new Vector2(field.X + field.Width * 0.28f, field.Center.Y - 18);

        DrawCircle(sb, qb, 3, Color.White);
        DrawCircle(sb, rb, 3, Color.White);
        DrawCircle(sb, wr1, 3, Color.White);
        DrawCircle(sb, wr2, 3, Color.White);
        DrawCircle(sb, te, 3, Color.White);

        // Routes/blocking (schematic):
        // - Yellow: routes
        // - Orange: blocking
        var slot = (off?.Slot ?? string.Empty).ToLowerInvariant();

        if (slot.StartsWith("pass"))
        {
            DrawLine(sb, qb, qb + new Vector2(18, 0), Color.Orange);
            DrawLine(sb, wr1, wr1 + new Vector2(52, -10), Color.Yellow);
            DrawLine(sb, wr2, wr2 + new Vector2(52, 10), Color.Yellow);
            DrawLine(sb, te, te + new Vector2(38, 18), Color.Yellow);
            DrawLine(sb, rb, rb + new Vector2(22, 24), Color.Yellow);
        }
        else
        {
            // Run / other.
            DrawLine(sb, qb, qb + new Vector2(12, 0), Color.Orange);
            DrawLine(sb, rb, rb + new Vector2(40, -10), Color.Yellow);
            DrawLine(sb, wr1, wr1 + new Vector2(26, 0), Color.Orange);
            DrawLine(sb, wr2, wr2 + new Vector2(26, 0), Color.Orange);
            DrawLine(sb, te, te + new Vector2(22, 0), Color.Orange);
        }
    }

    private void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color)
    {
        var dx = b - a;
        var len = dx.Length();
        if (len <= 0.01f)
            return;

        var angle = (float)Math.Atan2(dx.Y, dx.X);
        sb.Draw(_assets.Pixel, a, null, color, angle, Vector2.Zero, new Vector2(len, 1f), SpriteEffects.None, 0f);
    }

    private void DrawCircle(SpriteBatch sb, Vector2 c, int r, Color color)
    {
        sb.Draw(_assets.Pixel, new Rectangle((int)c.X - r, (int)c.Y - r, r * 2, r * 2), color);
    }

    private static string Truncate(string s, int max)
    {
        s = (s ?? string.Empty).Trim();
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }
}
