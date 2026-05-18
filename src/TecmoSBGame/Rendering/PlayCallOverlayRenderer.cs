using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.SimArch;
using TecmoSBGame.SimArch.Components.PlayCall;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Minimal demo-safe playcall overlay for the Arch runtime.
/// Keeps the current play-selection state visible without depending on Gum.
/// </summary>
public sealed class PlayCallOverlayRenderer
{
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, SimSnapshot snapshot)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));
        if (pixel is null)
            throw new ArgumentNullException(nameof(pixel));

        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font is null || !snapshot.PlayCall.Visible)
            return;

        var panel = new Rectangle(8, 36, 240, 84);
        DrawPanel(spriteBatch, pixel, panel);

        var offenseHeader = snapshot.PlayCall.Focus == PlayCallFocus.Formation
            ? "> FORMATION"
            : "  FORMATION";
        var playHeader = snapshot.PlayCall.Focus == PlayCallFocus.Play
            ? "> PLAY"
            : "  PLAY";

        spriteBatch.DrawString(font, "PLAY SELECT", new Vector2(panel.X + 8, panel.Y + 6), UiColors.TecmoGold);
        spriteBatch.DrawString(font, offenseHeader, new Vector2(panel.X + 8, panel.Y + 22), UiColors.TextWhite);
        spriteBatch.DrawString(font, snapshot.PlayCall.SelectedFormationIdOrFallback, new Vector2(panel.X + 86, panel.Y + 22), UiColors.Highlight);
        spriteBatch.DrawString(font, playHeader, new Vector2(panel.X + 8, panel.Y + 38), UiColors.TextWhite);
        spriteBatch.DrawString(font, snapshot.PlayCall.SelectedPlayNameOrFallback, new Vector2(panel.X + 86, panel.Y + 38), UiColors.Highlight);

        var formationWindow = string.Join("  ", snapshot.PlayCall.FormationWindow);
        var playWindow = string.Join("  ", snapshot.PlayCall.PlayWindow);
        spriteBatch.DrawString(font, formationWindow, new Vector2(panel.X + 8, panel.Y + 54), UiColors.TextGray);
        spriteBatch.DrawString(font, playWindow, new Vector2(panel.X + 8, panel.Y + 68), UiColors.TextGray);

        spriteBatch.DrawString(font, "ARROWS MOVE  ENTER SELECT  SPACE SNAP", new Vector2(panel.X + 8, panel.Y + 82), UiColors.Good);
    }

    private static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle panel)
    {
        spriteBatch.Draw(pixel, panel, UiColors.OverlayDim);
        spriteBatch.Draw(pixel, new Rectangle(panel.X, panel.Y, panel.Width, 1), UiColors.TecmoGold);
        spriteBatch.Draw(pixel, new Rectangle(panel.X, panel.Bottom - 1, panel.Width, 1), UiColors.TecmoGold);
        spriteBatch.Draw(pixel, new Rectangle(panel.X, panel.Y, 1, panel.Height), UiColors.TecmoGold);
        spriteBatch.Draw(pixel, new Rectangle(panel.Right - 1, panel.Y, 1, panel.Height), UiColors.TecmoGold);
    }
}
