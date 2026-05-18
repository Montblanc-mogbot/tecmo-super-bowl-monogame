using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.State;

namespace TecmoSBGame.Rendering.PostPlay;

/// <summary>
/// Renders a Tecmo-style post-play summary overlay.
/// Draw in the same SpriteBatch as the field/entities (virtual 256x224 coordinates).
/// </summary>
public sealed class PostPlaySummaryRenderer
{
    private readonly PanelRenderer _panels;
    private readonly InputPromptRenderer _prompts = new();
    private readonly Texture2D _pixel;

    public PostPlaySummaryRenderer(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice is null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        _panels = new PanelRenderer(graphicsDevice);

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, PlayState play, MatchState match, float timeSeconds)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));
        if (play is null)
            throw new ArgumentNullException(nameof(play));
        if (match is null)
            throw new ArgumentNullException(nameof(match));

        // Full-screen dim.
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, UiScaling.BaseWidth, UiScaling.BaseHeight), UiColors.OverlayDim);

        // Panel.
        var panel = new Rectangle(16, 44, UiScaling.BaseWidth - 32, 136);
        _panels.DrawTecmoBox(spriteBatch, panel);

        var titleFont = FontSystem.Instance.GetFont(FontSize.Large);
        var bodyFont = FontSystem.Instance.GetFont(FontSize.Medium);
        var smallFont = FontSystem.Instance.GetFont(FontSize.Small);

        // If fonts aren't loaded, we at least render the panel.
        if (titleFont is null || bodyFont is null)
            return;

        // Determine result label.
        var resultLabel = GetResultLabel(play, match);
        var resultColor = GetResultColor(play, match);

        var titlePos = new Vector2(panel.X + 12, panel.Y + 10);
        spriteBatch.DrawString(titleFont, resultLabel, titlePos, resultColor);

        if (smallFont is not null)
        {
            var metaText = $"PLAY {match.PlayNumber} · Q{Math.Clamp(match.Quarter, 1, 4)} · {MatchState.FormatClock(match.GameClockSeconds)}";
            spriteBatch.DrawString(smallFont, metaText, new Vector2(panel.Right - 12 - smallFont.MeasureString(metaText).X, panel.Y + 14), UiColors.TextGray);
        }

        // Yards gained/lost.
        var yards = play.Result.YardsGained;
        var yardsText = yards switch
        {
            > 0 => $"+{yards} YARDS",
            < 0 => $"{yards} YARDS",
            _ => "0 YARDS",
        };

        var yardsColor = yards switch
        {
            > 0 => UiColors.Good,
            < 0 => UiColors.Bad,
            _ => UiColors.TextGray,
        };

        if (play.Result.Touchdown || play.Result.Safety)
            yardsColor = UiColors.TecmoGold;

        var yardsPos = new Vector2(panel.X + 12, panel.Y + 48);
        spriteBatch.DrawString(bodyFont, yardsText, yardsPos, yardsColor);

        var detailText = BuildDetailText(play, match);
        if (smallFont is not null)
            spriteBatch.DrawString(smallFont, detailText, new Vector2(panel.X + 12, panel.Y + 76), UiColors.TextGray);

        // Updated down/distance and spot.
        var dd = match.FormatDownDistance();
        var spot = match.BallSpot.ToString();
        var ddText = $"NEXT: {dd}  BALL: {spot}";
        var ddPos = new Vector2(panel.X + 12, panel.Y + 94);
        spriteBatch.DrawString(bodyFont, ddText, ddPos, UiColors.TextWhite);

        // First down indicator heuristic: match is already advanced; if Down reset to 1 after a non-score/non-turnover gain.
        var firstDownAchieved = match.Down == 1 && !play.Result.Touchdown && !play.Result.Safety && !play.Result.Turnover && play.Result.YardsGained > 0;
        if (firstDownAchieved)
        {
            var fdPos = new Vector2(panel.Right - 12, panel.Y + 10);
            var fdText = "1ST DOWN";
            var size = titleFont.MeasureString(fdText);
            spriteBatch.DrawString(titleFont, fdText, new Vector2(fdPos.X - size.X, fdPos.Y), UiColors.Highlight);
        }

        // Prompt.
        if (!play.AutoDismissPostPlay)
        {
            var promptPos = new Vector2(panel.X + 12, panel.Bottom - 24);
            _prompts.DrawPressStart(spriteBatch, promptPos, timeSeconds);
        }
        else if (smallFont is not null)
        {
            var infoPos = new Vector2(panel.X + 12, panel.Bottom - 20);
            spriteBatch.DrawString(smallFont, "CONTINUING...", infoPos, UiColors.TextGray);
        }
    }

    private static string BuildDetailText(PlayState play, MatchState match)
    {
        if (play.Result.Touchdown)
            return "SCORING PLAY · KICKOFF COMING";
        if (play.Result.Safety)
            return "2 POINTS · CHANGE OF POSSESSION";
        if (play.Result.Turnover)
            return "POSSESSION FLIPS AT THE DEAD-BALL SPOT";
        if (play.WhistleReason == WhistleReason.Incomplete)
            return "BALL RETURNS TO THE PREVIOUS SPOT";
        if (match.Down == 1 && play.Result.YardsGained > 0)
            return "CHAINS MOVED · OFFENSE STAYS ON SCHEDULE";
        if (play.Result.YardsGained < 0)
            return "DEFENSE WON THE DOWN";
        return "READY FOR THE NEXT SNAP";
    }

    private static string GetResultLabel(PlayState play, MatchState match)
    {
        if (play.Result.Touchdown)
            return "TOUCHDOWN";
        if (play.Result.Safety)
            return "SAFETY";
        if (play.WhistleReason == WhistleReason.Incomplete)
            return "INCOMPLETE";
        if (play.Result.Turnover)
            return "TURNOVER";

        // Heuristic first down: if the match down reset to 1 and it's not a scoring/turnover play.
        if (match.Down == 1 && play.Result.YardsGained > 0)
            return "FIRST DOWN";

        return "PLAY OVER";
    }

    private static Color GetResultColor(PlayState play, MatchState match)
    {
        if (play.Result.Touchdown || play.Result.Safety)
            return UiColors.TecmoGold;
        if (play.Result.Turnover)
            return UiColors.Bad;
        if (match.Down == 1 && play.Result.YardsGained > 0)
            return UiColors.Good;
        if (play.WhistleReason == WhistleReason.Incomplete)
            return UiColors.TextGray;

        return UiColors.TextWhite;
    }
}
