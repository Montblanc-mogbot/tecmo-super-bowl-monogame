using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.State;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Simple in-game debug overlay for assembly-parity work.
///
/// Rendered in world-space coordinates (virtual 256x224) so it scales with the viewport.
/// </summary>
public sealed class DebugOverlayRenderer
{
    public bool Enabled { get; set; }

    public void Draw(SpriteBatch spriteBatch, RenderResources resources, MatchState match, PlayState play)
    {
        if (!Enabled)
            return;

        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font is null)
            return;

        // Backdrop
        var bg = new Rectangle(4, 4, 248, 54);
        spriteBatch.Draw(resources.Pixel, bg, new Color(0, 0, 0, 170));

        var y = 8f;
        DrawLine(spriteBatch, font, 8f, ref y, $"flow: playId={play.PlayId} phase={play.Phase} allowPass={play.AllowPass} over={play.IsOver}");
        DrawLine(spriteBatch, font, 8f, ref y, $"match: poss={match.PossessionTeam} dir={match.OffenseDirection} down={match.Down} ytg={match.YardsToGo} spot={match.BallSpot}");
        DrawLine(spriteBatch, font, 8f, ref y, $"ball: owner={(play.BallOwnerEntityId is null ? "none" : play.BallOwnerEntityId.Value.ToString())} state={play.BallState} absYd={play.EndAbsoluteYard} gained={play.Result.YardsGained}");
        DrawLine(spriteBatch, font, 8f, ref y, $"timers: t={play.PlayElapsedSeconds:0.00}s seed=0x{play.DeterministicSeed:X8}");
    }

    private static void DrawLine(SpriteBatch sb, SpriteFont font, float x, ref float y, string text)
    {
        sb.DrawString(font, text, new Vector2(x, y), Color.White);
        y += font.LineSpacing;
    }
}
