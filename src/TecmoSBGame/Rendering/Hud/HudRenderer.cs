using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.SimArch;

namespace TecmoSBGame.Rendering.Hud;

/// <summary>
/// Player-readable HUD overlay for SimArch runtime.
/// </summary>
public sealed class HudRenderer
{
    private readonly PanelRenderer _panels;
    private readonly InputPromptRenderer _prompts = new();

    public HudRenderer(GraphicsDevice graphicsDevice)
    {
        _panels = new PanelRenderer(graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice)));
    }

    public void Draw(SpriteBatch spriteBatch, SimSnapshot snapshot)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));

        var font = FontSystem.Instance.GetFont(FontSize.Small);
        var mediumFont = FontSystem.Instance.GetFont(FontSize.Medium);
        if (font is null || mediumFont is null)
            return;

        var hud = snapshot.Hud;
        var topBar = new Rectangle(6, 4, UiScaling.BaseWidth - 12, 34);
        _panels.DrawPanel(spriteBatch, topBar, new Color(0, 0, 0, 170), UiColors.TecmoGold, 1);

        var awayLabel = BuildTeamLabel("AWAY", hud.AwayTeamId, hud.PossessionTeam == 0);
        var homeLabel = BuildTeamLabel("HOME", hud.HomeTeamId, hud.PossessionTeam == 1);
        var clock = FormatClock(hud.GameClockSeconds);
        var quarter = $"Q{Math.Clamp(hud.Quarter, 1, 4)}";

        spriteBatch.DrawString(font, awayLabel, new Vector2(12, 8), hud.PossessionTeam == 0 ? UiColors.Highlight : UiColors.TextWhite);
        DrawRightAligned(spriteBatch, font, hud.Team0Score.ToString(), new Vector2(102, 8), UiColors.TextWhite);
        DrawCentered(spriteBatch, mediumFont, quarter, new Vector2(UiScaling.BaseWidth / 2f, 9), UiColors.TecmoGold);
        DrawCentered(spriteBatch, font, clock, new Vector2(UiScaling.BaseWidth / 2f, 24), UiColors.TextWhite);
        spriteBatch.DrawString(font, homeLabel, new Vector2(152, 8), hud.PossessionTeam == 1 ? UiColors.Highlight : UiColors.TextWhite);
        DrawRightAligned(spriteBatch, font, hud.Team1Score.ToString(), new Vector2(244, 8), UiColors.TextWhite);

        var infoPanel = new Rectangle(6, 40, UiScaling.BaseWidth - 12, 24);
        _panels.DrawPanel(spriteBatch, infoPanel, new Color(0, 0, 0, 130), UiColors.TecmoBlue, 1);
        spriteBatch.DrawString(font, hud.SituationLabel ?? string.Empty, new Vector2(12, 45), UiColors.TextWhite);
        DrawRightAligned(spriteBatch, font, hud.PossessionLabel ?? string.Empty, new Vector2(244, 45), UiColors.TextGray);

        if (!string.IsNullOrWhiteSpace(hud.StatusLine))
        {
            var statusColor = hud.Paused ? UiColors.Highlight : hud.MatchOver ? UiColors.TecmoGold : UiColors.TextGray;
            spriteBatch.DrawString(font, hud.StatusLine, new Vector2(12, 68), statusColor);
        }

        if (!string.IsNullOrWhiteSpace(hud.LastPlaySummary))
        {
            var summaryPanel = new Rectangle(6, UiScaling.BaseHeight - 52, UiScaling.BaseWidth - 12, 20);
            _panels.DrawPanel(spriteBatch, summaryPanel, new Color(0, 0, 0, 150), UiColors.TecmoGold, 1);
            DrawCentered(spriteBatch, font, hud.LastPlaySummary, new Vector2(summaryPanel.Center.X, summaryPanel.Y + 6), UiColors.TextWhite);
        }

        if (hud.Paused)
        {
            var pausedPanel = new Rectangle(62, 92, 132, 40);
            _panels.DrawTecmoBox(spriteBatch, pausedPanel);
            DrawCentered(spriteBatch, mediumFont, "PAUSED", new Vector2(pausedPanel.Center.X, pausedPanel.Y + 8), UiColors.Highlight);
            _prompts.DrawButtonPrompt(spriteBatch, "P", "RESUME", new Vector2(pausedPanel.X + 30, pausedPanel.Y + 24));
        }
    }

    private static string BuildTeamLabel(string side, int teamId, bool hasBall)
        => hasBall ? $"> {side} {teamId}" : $"  {side} {teamId}";

    private static void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 center, Color color)
    {
        var size = font.MeasureString(text);
        spriteBatch.DrawString(font, text, new Vector2(center.X - size.X / 2f, center.Y), color);
    }

    private static void DrawRightAligned(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 right, Color color)
    {
        var size = font.MeasureString(text);
        spriteBatch.DrawString(font, text, new Vector2(right.X - size.X, right.Y), color);
    }

    private static string FormatClock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }
}
