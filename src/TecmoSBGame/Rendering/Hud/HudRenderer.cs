using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.SimArch;

namespace TecmoSBGame.Rendering.Hud;

/// <summary>
/// Minimal HUD renderer for SimArch.
/// Renders: score, quarter/clock, down&distance, ball spot.
/// </summary>
public sealed class HudRenderer
{
    public void Draw(SpriteBatch spriteBatch, SimSnapshot snapshot)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));

        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font is null)
            return;

        var hud = snapshot.Hud;

        var clock = FormatClock(hud.GameClockSeconds);
        var dd = FormatDownDistance(hud.Down, hud.YardsToGo);
        var spot = hud.BallOnOwnSide ? $"OWN {hud.BallYards}" : $"OPP {hud.BallYards}";

        var line1 = $"Q{hud.Quarter}  {clock}    {hud.Team0Score} - {hud.Team1Score}";
        var line2 = $"{dd}   BALL: {spot}";

        spriteBatch.DrawString(font, line1, new Vector2(6, 6), UiColors.TextWhite);
        spriteBatch.DrawString(font, line2, new Vector2(6, 20), UiColors.TextWhite);
    }

    private static string FormatDownDistance(int down, int ytg)
    {
        var d = down switch
        {
            1 => "1ST",
            2 => "2ND",
            3 => "3RD",
            _ => "4TH",
        };

        return $"{d}&{ytg}";
    }

    private static string FormatClock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }
}
