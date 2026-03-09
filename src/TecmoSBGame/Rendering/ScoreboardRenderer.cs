using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.State;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders a Tecmo-style scoreboard strip at the top of the screen.
/// Uses the 256x224 virtual resolution (RenderViewport transform should already be applied).
/// </summary>
public sealed class ScoreboardRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _pixel;
    private readonly NesHudFont _font = new();

    // Tecmo-ish palette.
    private static readonly Color TecmoBlue = new(0x00, 0x66, 0xCC);
    private static readonly Color TecmoGold = new(0xFF, 0xCC, 0x00);

    public ScoreboardRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
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

    public void Draw(SpriteBatch sb, MatchState match)
    {
        if (sb is null)
            throw new ArgumentNullException(nameof(sb));
        if (match is null)
            throw new ArgumentNullException(nameof(match));

        // Layout within NES resolution.
        var bar = new Rectangle(0, 0, 256, 28);
        sb.Draw(Pixel, bar, TecmoBlue);

        // Inner border
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 1), Color.White);
        sb.Draw(Pixel, new Rectangle(0, 27, 256, 1), Color.White);

        // Team plates (placeholder colors; later derived from team data)
        var awayPlate = new Rectangle(2, 2, 90, 12);
        var homePlate = new Rectangle(164, 2, 90, 12);
        sb.Draw(Pixel, awayPlate, new Color(20, 40, 90));
        sb.Draw(Pixel, homePlate, new Color(60, 20, 90));

        // Center quarter/clock box
        var mid = new Rectangle(98, 2, 60, 24);
        sb.Draw(Pixel, mid, new Color(0, 0, 0, 110));
        sb.Draw(Pixel, new Rectangle(mid.X, mid.Y, mid.Width, 1), TecmoGold);
        sb.Draw(Pixel, new Rectangle(mid.X, mid.Y + mid.Height - 1, mid.Width, 1), TecmoGold);

        // Text values
        var awayScore = match.Team0Score.ToString();
        var homeScore = match.Team1Score.ToString();
        var quarter = $"Q{Math.Clamp(match.Quarter, 1, 4)}";
        var clock = MatchState.FormatClock(match.GameClockSeconds);

        // Scores: right aligned within plates.
        DrawRightAligned(sb, awayScore, new Vector2(awayPlate.Right - 4, awayPlate.Y + 3), Color.White, scale: 1);
        DrawRightAligned(sb, homeScore, new Vector2(homePlate.Right - 4, homePlate.Y + 3), Color.White, scale: 1);

        // Placeholder team labels.
        _font.Draw(sb, Pixel, "AWAY", new Vector2(awayPlate.X + 4, awayPlate.Y + 3), new Color(220, 220, 220), scale: 1);
        _font.Draw(sb, Pixel, "HOME", new Vector2(homePlate.X + 4, homePlate.Y + 3), new Color(220, 220, 220), scale: 1);

        // Quarter and clock centered in mid box.
        DrawCentered(sb, quarter, new Vector2(mid.Center.X, mid.Y + 4), TecmoGold, scale: 1);
        DrawCentered(sb, clock, new Vector2(mid.Center.X, mid.Y + 14), Color.White, scale: 1);
    }

    private void DrawCentered(SpriteBatch sb, string text, Vector2 center, Color color, int scale)
    {
        var size = _font.Measure(text, scale);
        var pos = new Vector2((int)(center.X - (size.X / 2f)), (int)(center.Y - (size.Y / 2f)));
        _font.Draw(sb, Pixel, text, pos, color, scale);
    }

    private void DrawRightAligned(SpriteBatch sb, string text, Vector2 rightAnchor, Color color, int scale)
    {
        var size = _font.Measure(text, scale);
        var pos = new Vector2((int)(rightAnchor.X - size.X), (int)rightAnchor.Y);
        _font.Draw(sb, Pixel, text, pos, color, scale);
    }
}
