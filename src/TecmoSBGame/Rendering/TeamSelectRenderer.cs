using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSB;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Team select screen renderer.
/// Virtual resolution: 256x224 (NES).
/// </summary>
public sealed class TeamSelectRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GameContent _content;
    private readonly NesHudFont _font = new();
    private Texture2D? _pixel;

    public TeamSelectRenderer(GraphicsDevice graphicsDevice, GameContent content)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    private Texture2D Pixel
    {
        get
        {
            if (_pixel is not null)
                return _pixel;

            var tex = new Texture2D(_graphicsDevice, 1, 1);
            tex.SetData(new[] { Color.White });
            _pixel = tex;
            return tex;
        }
    }

    public void Draw(SpriteBatch sb, int awayIndex, int homeIndex, int activeColumn)
    {
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 102, 204));

        // Frame.
        sb.Draw(Pixel, new Rectangle(16, 16, 224, 192), new Color(0, 0, 0, 180));
        sb.Draw(Pixel, new Rectangle(18, 18, 220, 188), new Color(12, 28, 80));

        // Column layouts.
        var awayRect = new Rectangle(24, 40, 104, 136);
        var homeRect = new Rectangle(128, 40, 104, 136);

        DrawColumn(sb, awayRect, title: "AWAY", teamIndex: awayIndex, isActive: activeColumn == 0, playerLabel: "P1");
        DrawColumn(sb, homeRect, title: "HOME", teamIndex: homeIndex, isActive: activeColumn == 1, playerLabel: "P2");

        // Preview panels.
        var previewRect = new Rectangle(24, 180, 208, 24);
        sb.Draw(Pixel, previewRect, new Color(0, 0, 0, 120));

        var activeIdx = activeColumn == 0 ? awayIndex : homeIndex;
        var team = SafeGetTeam(activeIdx);
        var name = team is null ? "TEAM" : (team.City + " " + team.Name);
        _font.Draw(sb, Pixel, name.ToUpperInvariant(), new Vector2(28, 186), Color.White, scale: 1, spacing: 1);

        DrawRatings(sb, teamId: activeIdx, origin: new Vector2(28, 196));
    }

    private void DrawColumn(SpriteBatch sb, Rectangle rect, string title, int teamIndex, bool isActive, string playerLabel)
    {
        // Header.
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y - 16, rect.Width, 14), new Color(0, 0, 0, 120));
        _font.Draw(sb, Pixel, title, new Vector2(rect.X + 4, rect.Y - 14), Color.White);

        // Active indicator.
        if (isActive)
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y - 16, rect.Width, 2), new Color(255, 220, 120));

        // Player assignment tag.
        sb.Draw(Pixel, new Rectangle(rect.Right - 24, rect.Y - 16, 24, 14), new Color(0, 0, 0, 120));
        _font.Draw(sb, Pixel, playerLabel, new Vector2(rect.Right - 22, rect.Y - 14), new Color(255, 220, 120));

        // List body.
        sb.Draw(Pixel, rect, new Color(0, 0, 0, 80));

        const int rowH = 12;
        int visibleRows = rect.Height / rowH; // 11 rows

        int count = _content.TeamData.Teams.Count;
        if (count <= 0)
            return;

        // Scroll window centered around selection.
        int start = teamIndex - (visibleRows / 2);
        if (start < 0) start = 0;
        if (start > count - visibleRows) start = Math.Max(0, count - visibleRows);

        for (int i = 0; i < visibleRows; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= count)
                continue;

            int y = rect.Y + i * rowH;
            bool selected = idx == teamIndex;

            var back = selected ? new Color(255, 220, 120) : new Color(0, 0, 0, 0);
            var text = selected ? new Color(40, 40, 60) : Color.White;

            if (selected)
                sb.Draw(Pixel, new Rectangle(rect.X + 2, y + 1, rect.Width - 4, rowH - 2), back);

            // Team abbrev.
            var team = _content.TeamData.Teams[idx];
            _font.Draw(sb, Pixel, team.Abbrev.ToUpperInvariant(), new Vector2(rect.X + 6, y + 3), text);

            // Little cursor marker.
            if (selected)
                sb.Draw(Pixel, new Rectangle(rect.Right - 10, y + 4, 6, 6), Color.White);
        }

        // Scroll bar.
        if (count > visibleRows)
        {
            int barX = rect.Right - 4;
            sb.Draw(Pixel, new Rectangle(barX, rect.Y + 2, 2, rect.Height - 4), new Color(255, 255, 255, 40));

            float t = teamIndex / (float)(count - 1);
            int knobY = rect.Y + 2 + (int)MathF.Round(t * (rect.Height - 8));
            sb.Draw(Pixel, new Rectangle(barX, knobY, 2, 6), new Color(255, 220, 120));
        }
    }

    private TeamDefinition? SafeGetTeam(int teamIndex)
    {
        var teams = _content.TeamData.Teams;
        if (teamIndex < 0 || teamIndex >= teams.Count)
            return null;
        return teams[teamIndex];
    }

    private void DrawRatings(SpriteBatch sb, int teamId, Vector2 origin)
    {
        // Placeholder ratings: deterministic, 0..100.
        int off = Rating(teamId, 13);
        int def = Rating(teamId, 37);
        int ovr = (off + def) / 2;

        DrawRatingRow(sb, origin + new Vector2(0, 0), "OFF", off, new Color(120, 220, 120));
        DrawRatingRow(sb, origin + new Vector2(70, 0), "DEF", def, new Color(120, 180, 255));
        DrawRatingRow(sb, origin + new Vector2(140, 0), "OVR", ovr, new Color(255, 220, 120));
    }

    private void DrawRatingRow(SpriteBatch sb, Vector2 pos, string label, int value, Color fill)
    {
        _font.Draw(sb, Pixel, label, pos, Color.White);

        int barX = (int)pos.X + 18;
        int barY = (int)pos.Y + 1;
        int barW = 44;
        int barH = 5;

        sb.Draw(Pixel, new Rectangle(barX, barY, barW, barH), new Color(0, 0, 0, 120));
        int w = (int)MathF.Round(barW * (Math.Clamp(value, 0, 100) / 100f));
        sb.Draw(Pixel, new Rectangle(barX, barY, w, barH), fill);
    }

    private static int Rating(int teamId, int salt)
    {
        unchecked
        {
            var v = (teamId + 1) * 1103515245 + salt * 12345;
            v ^= (v >> 16);
            v &= 0x7FFFFFFF;
            return v % 101;
        }
    }
}
