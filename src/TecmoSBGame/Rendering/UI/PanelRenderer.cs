using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering.UI;

/// <summary>
/// Shared utility for drawing simple UI panels/boxes using a 1x1 pixel texture.
/// Provides a "Tecmo" styled box helper.
///
/// 9-slice scaling is supported if you provide a texture and source rectangles.
/// (Not currently wired to content; this class keeps the API ready.)
/// </summary>
public sealed class PanelRenderer
{
    private readonly Texture2D _pixel;

    public PanelRenderer(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice is null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color fill, Color? border = null, int borderThickness = 2)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));

        // Fill
        spriteBatch.Draw(_pixel, rect, fill);

        if (border is null)
            return;

        DrawBorder(spriteBatch, rect, border.Value, borderThickness);
    }

    public void DrawTecmoBox(SpriteBatch spriteBatch, Rectangle rect)
    {
        // Slight shadow for contrast.
        var shadow = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height);
        spriteBatch.Draw(_pixel, shadow, new Color(0, 0, 0, 120));

        DrawPanel(spriteBatch, rect, UiColors.TecmoBlue, UiColors.TecmoGold, borderThickness: 2);

        // Inner highlight border.
        var inner = Inflate(rect, -3);
        DrawBorder(spriteBatch, inner, new Color(255, 255, 255, 60), thickness: 1);
    }

    public void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 1)
    {
        if (thickness <= 0)
            return;

        // Top
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        // Left
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        // Right
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    /// <summary>
    /// Minimal 9-slice draw helper.
    /// If <paramref name="texture"/> is null this method does nothing.
    /// </summary>
    public void DrawNineSlice(SpriteBatch spriteBatch, Texture2D? texture, Rectangle dest, Rectangle srcCenter, int edge)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));
        if (texture is null)
            return;

        // Source rects
        var srcTL = new Rectangle(srcCenter.X - edge, srcCenter.Y - edge, edge, edge);
        var srcT = new Rectangle(srcCenter.X, srcCenter.Y - edge, srcCenter.Width, edge);
        var srcTR = new Rectangle(srcCenter.Right, srcCenter.Y - edge, edge, edge);

        var srcL = new Rectangle(srcCenter.X - edge, srcCenter.Y, edge, srcCenter.Height);
        var srcC = srcCenter;
        var srcR = new Rectangle(srcCenter.Right, srcCenter.Y, edge, srcCenter.Height);

        var srcBL = new Rectangle(srcCenter.X - edge, srcCenter.Bottom, edge, edge);
        var srcB = new Rectangle(srcCenter.X, srcCenter.Bottom, srcCenter.Width, edge);
        var srcBR = new Rectangle(srcCenter.Right, srcCenter.Bottom, edge, edge);

        // Destination rects
        var dstTL = new Rectangle(dest.X, dest.Y, edge, edge);
        var dstT = new Rectangle(dest.X + edge, dest.Y, dest.Width - edge * 2, edge);
        var dstTR = new Rectangle(dest.Right - edge, dest.Y, edge, edge);

        var dstL = new Rectangle(dest.X, dest.Y + edge, edge, dest.Height - edge * 2);
        var dstC = new Rectangle(dest.X + edge, dest.Y + edge, dest.Width - edge * 2, dest.Height - edge * 2);
        var dstR = new Rectangle(dest.Right - edge, dest.Y + edge, edge, dest.Height - edge * 2);

        var dstBL = new Rectangle(dest.X, dest.Bottom - edge, edge, edge);
        var dstB = new Rectangle(dest.X + edge, dest.Bottom - edge, dest.Width - edge * 2, edge);
        var dstBR = new Rectangle(dest.Right - edge, dest.Bottom - edge, edge, edge);

        spriteBatch.Draw(texture, dstTL, srcTL, Color.White);
        spriteBatch.Draw(texture, dstT, srcT, Color.White);
        spriteBatch.Draw(texture, dstTR, srcTR, Color.White);

        spriteBatch.Draw(texture, dstL, srcL, Color.White);
        spriteBatch.Draw(texture, dstC, srcC, Color.White);
        spriteBatch.Draw(texture, dstR, srcR, Color.White);

        spriteBatch.Draw(texture, dstBL, srcBL, Color.White);
        spriteBatch.Draw(texture, dstB, srcB, Color.White);
        spriteBatch.Draw(texture, dstBR, srcBR, Color.White);
    }

    private static Rectangle Inflate(Rectangle r, int amount)
    {
        return new Rectangle(r.X - amount, r.Y - amount, r.Width + amount * 2, r.Height + amount * 2);
    }
}
