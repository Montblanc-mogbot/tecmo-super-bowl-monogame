using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSB;
using TecmoSBGame.Components.PlayCall;

namespace TecmoSBGame.Rendering.PlayCall;

public sealed class FormationSelectRenderer
{
    private readonly PlayCallUiAssets _assets;
    private readonly FormationDataConfig _formationData;

    public FormationSelectRenderer(PlayCallUiAssets assets, FormationDataConfig formationData)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _formationData = formationData ?? throw new ArgumentNullException(nameof(formationData));
    }

    public void Draw(SpriteBatch sb, Rectangle area, PlayCallComponent pc)
    {
        if (!pc.Visible)
            return;

        // Background.
        var bg = new Color(24, 40, 120); // NES-ish blue
        sb.Draw(_assets.Pixel, area, bg);

        var headerRect = new Rectangle(area.X, area.Y, area.Width, 22);
        sb.Draw(_assets.Pixel, headerRect, new Color(16, 28, 96));
        sb.DrawString(_assets.Font, "FORMATION", new Vector2(area.X + 6, area.Y + 3), Color.White);

        var content = new Rectangle(area.X + 6, area.Y + 26, area.Width - 12, area.Height - 32);

        const int cols = 4;
        const int rows = 3;
        var cellW = content.Width / cols;
        var cellH = content.Height / rows;

        for (var i = 0; i < Math.Min(pc.FormationIds.Count, cols * rows); i++)
        {
            var col = i % cols;
            var row = i / cols;

            var cell = new Rectangle(content.X + col * cellW, content.Y + row * cellH, cellW - 4, cellH - 4);
            var selected = i == pc.FormationIndex && pc.Step == PlayCallStep.Offense && pc.Focus == PlayCallFocus.Formation;

            var fill = selected ? new Color(212, 175, 55) : new Color(40, 64, 160);
            sb.Draw(_assets.Pixel, cell, fill);

            var id = pc.FormationIds[i];
            var name = FindFormationName(id);
            var abbrev = Abbrev(name, max: 6);

            sb.DrawString(_assets.Font, abbrev, new Vector2(cell.X + 4, cell.Y + 4), selected ? Color.Black : Color.White);
            sb.DrawString(_assets.Font, Abbrev(id, max: 4), new Vector2(cell.X + 4, cell.Y + 18), selected ? Color.Black : Color.White);
        }

        // Footer hint.
        var hint = "A:plays  TAB:focus  SHIFT:DEF";
        sb.DrawString(_assets.Font, hint, new Vector2(area.X + 6, area.Bottom - 18), Color.White);
    }

    private string FindFormationName(string id)
    {
        foreach (var f in _formationData.OffensiveFormations)
        {
            if (string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase))
                return f.Name;
        }

        return id;
    }

    private static string Abbrev(string s, int max)
    {
        s = (s ?? string.Empty).Trim();
        if (s.Length <= max)
            return s;
        return s[..max];
    }
}
