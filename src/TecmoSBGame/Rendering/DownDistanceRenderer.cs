using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.State;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders down/distance + ball spot information at the bottom of the screen.
/// Uses the 256x224 virtual resolution (RenderViewport transform should already be applied).
/// </summary>
public sealed class DownDistanceRenderer
{
    private Texture2D? _pixel;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly NesHudFont _font = new();

    private static readonly Color TecmoBlue = new(0x00, 0x66, 0xCC);
    private static readonly Color TecmoGold = new(0xFF, 0xCC, 0x00);

    public DownDistanceRenderer(GraphicsDevice graphicsDevice)
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

        // Bottom panel (keep some room above for post-play overlays etc.)
        var panel = new Rectangle(0, 224 - 30, 256, 30);
        sb.Draw(Pixel, panel, new Color(0, 0, 0, 140));

        // Top border
        sb.Draw(Pixel, new Rectangle(0, panel.Y, 256, 1), TecmoBlue);

        var downText = FormatDown(match.Down);
        var yardsText = FormatYardsToGo(match.YardsToGo);
        var spotText = FormatBallSpot(match);

        // Left: down & distance
        _font.Draw(sb, Pixel, downText, new Vector2(6, panel.Y + 6), TecmoGold);
        _font.Draw(sb, Pixel, "&", new Vector2(6 + _font.Measure(downText).X + 2, panel.Y + 6), Color.White);
        _font.Draw(sb, Pixel, yardsText, new Vector2(6 + _font.Measure(downText).X + 2 + _font.Measure("&").X + 2, panel.Y + 6), Color.White);

        // Right: ball spot string
        DrawRightAligned(sb, spotText, new Vector2(250, panel.Y + 6), Color.White);

        // Field position bar
        DrawFieldPositionBar(sb, match, new Rectangle(6, panel.Y + 18, 244, 8));
    }

    private void DrawFieldPositionBar(SpriteBatch sb, MatchState match, Rectangle rect)
    {
        // Base bar
        sb.Draw(Pixel, rect, new Color(10, 50, 10));
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.White);
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.White);

        // Midfield marker
        var midX = rect.X + (rect.Width / 2);
        sb.Draw(Pixel, new Rectangle(midX, rect.Y, 1, rect.Height), new Color(255, 255, 255, 120));

        // Ball marker position
        var abs = BallSpotToAbsoluteYard0To100(match);
        var t = abs / 100f;
        var x = rect.X + (int)MathF.Round(t * (rect.Width - 1));

        // Draw marker (3px wide)
        sb.Draw(Pixel, new Rectangle(x - 1, rect.Y, 3, rect.Height), TecmoGold);
    }

    private static int BallSpotToAbsoluteYard0To100(MatchState match)
    {
        // Absolute yard is always from the left goal line.
        var spot = match.BallSpot;

        if (spot.Side == FieldSide.Midfield)
            return 50;

        // Convert spot (relative to offense) to distance from offense's own goal.
        var distFromOwnGoal = spot.Side == FieldSide.Own ? spot.YardLine : 100 - spot.YardLine;

        // Now convert to absolute from left goal based on offense direction.
        return match.OffenseDirection == OffenseDirection.LeftToRight
            ? distFromOwnGoal
            : 100 - distFromOwnGoal;
    }

    private static string FormatDown(int down)
    {
        down = Math.Max(1, down);
        return down switch
        {
            1 => "1ST",
            2 => "2ND",
            3 => "3RD",
            4 => "4TH",
            _ => $"{down}TH",
        };
    }

    private static string FormatYardsToGo(int yardsToGo)
    {
        if (yardsToGo <= 0)
            return "GOAL";

        return yardsToGo.ToString();
    }

    private static string FormatBallSpot(MatchState match)
    {
        // Placeholder until we have team abbreviations.
        var team = match.PossessionTeam == 0 ? "T0" : "T1";

        return match.BallSpot.Side switch
        {
            FieldSide.Midfield => "50",
            FieldSide.Own => $"{team} {match.BallSpot.YardLine}",
            FieldSide.Opp => $"OPP {match.BallSpot.YardLine}",
            _ => match.BallSpot.ToString(),
        };
    }

    private void DrawRightAligned(SpriteBatch sb, string text, Vector2 rightAnchor, Color color, int scale = 1)
    {
        var size = _font.Measure(text, scale);
        var pos = new Vector2((int)(rightAnchor.X - size.X), (int)rightAnchor.Y);
        _font.Draw(sb, Pixel, text, pos, color, scale);
    }
}
