using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Components.PlayCall;

namespace TecmoSBGame.Rendering.PlayCall;

public sealed class DefensivePlaySelectRenderer
{
    private readonly PlayCallUiAssets _assets;

    public DefensivePlaySelectRenderer(PlayCallUiAssets assets)
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
        sb.DrawString(_assets.Font, "DEFENSE", new Vector2(area.X + 6, area.Y + 3), Color.White);

        var content = new Rectangle(area.X + 6, area.Y + 26, area.Width - 12, area.Height - 32);

        const int cols = 4;
        const int rows = 3;
        var cellW = content.Width / cols;
        var cellH = content.Height / rows;

        for (var i = 0; i < Math.Min(pc.DefensiveCalls.Count, cols * rows); i++)
        {
            var col = i % cols;
            var row = i / cols;

            var cell = new Rectangle(content.X + col * cellW, content.Y + row * cellH, cellW - 4, cellH - 4);
            var selected = i == pc.DefenseIndex && pc.Step == PlayCallStep.Defense;

            sb.Draw(_assets.Pixel, cell, selected ? new Color(212, 175, 55) : new Color(40, 64, 160));

            var d = pc.DefensiveCalls[i];
            var id = (d.Id ?? "").Trim();
            var desc = (d.Description ?? "").Trim();

            sb.DrawString(_assets.Font, Truncate(id, 8), new Vector2(cell.X + 4, cell.Y + 4), selected ? Color.Black : Color.White);
            sb.DrawString(_assets.Font, Truncate(CoverageHint(desc), 10), new Vector2(cell.X + 4, cell.Y + 18), selected ? Color.Black : Color.White);
        }

        var hint = "A/START:OK  SHIFT:OFF  B:BACK";
        sb.DrawString(_assets.Font, hint, new Vector2(area.X + 6, area.Bottom - 18), Color.White);
    }

    private static string CoverageHint(string desc)
    {
        var d = (desc ?? string.Empty).ToLowerInvariant();
        if (d.Contains("blitz")) return "BLITZ";
        if (d.Contains("man")) return "MAN";
        if (d.Contains("zone")) return "ZONE";
        if (d.Contains("nickel")) return "NICKEL";
        if (d.Contains("dime")) return "DIME";
        return "CALL";
    }

    private static string Truncate(string s, int max)
    {
        s = (s ?? string.Empty).Trim();
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }
}
