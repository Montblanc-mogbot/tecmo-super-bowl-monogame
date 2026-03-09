using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Components.PlayCall;

namespace TecmoSBGame.Rendering.PlayCall;

public sealed class PlaySelectRenderer
{
    private readonly PlayCallUiAssets _assets;

    public PlaySelectRenderer(PlayCallUiAssets assets)
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

        var title = $"PLAYS ({pc.PlaysForFormation.Count})";
        sb.DrawString(_assets.Font, title, new Vector2(area.X + 6, area.Y + 3), Color.White);

        var listArea = new Rectangle(area.X + 6, area.Y + 26, area.Width - 12, area.Height - 32);

        const int rowH = 18;
        var maxRows = Math.Max(1, listArea.Height / rowH);

        // Scroll so the selected play stays visible.
        var selected = pc.PlayIndex;
        var scrollTop = Math.Clamp(selected - maxRows / 2, 0, Math.Max(0, pc.PlaysForFormation.Count - maxRows));

        for (var r = 0; r < maxRows; r++)
        {
            var idx = scrollTop + r;
            if (idx >= pc.PlaysForFormation.Count)
                break;

            var y = listArea.Y + r * rowH;
            var rowRect = new Rectangle(listArea.X, y, listArea.Width, rowH - 2);

            var isSel = idx == pc.PlayIndex && pc.Step == PlayCallStep.Offense && pc.Focus == PlayCallFocus.Play;
            if (isSel)
                sb.Draw(_assets.Pixel, rowRect, new Color(212, 175, 55));

            var p = pc.PlaysForFormation[idx];
            var name = (p.Name ?? string.Empty).Trim();
            var slot = (p.Slot ?? string.Empty).Trim();

            var text = string.IsNullOrWhiteSpace(slot) ? name : $"{slot}: {name}";
            sb.DrawString(_assets.Font, Truncate(text, 24), new Vector2(rowRect.X + 4, rowRect.Y + 2), isSel ? Color.Black : Color.White);
        }

        var hint = "A:DEF  B:FORM";
        sb.DrawString(_assets.Font, hint, new Vector2(area.X + 6, area.Bottom - 18), Color.White);
    }

    private static string Truncate(string s, int max)
    {
        s = (s ?? string.Empty).Trim();
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }
}
