using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Simple main menu renderer (non-ECS). Uses rectangles only (no fonts required).
/// Virtual resolution: 256x224 (NES).
/// </summary>
public sealed class MainMenuRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _pixel;

    public MainMenuRenderer(GraphicsDevice graphicsDevice)
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

    /// <summary>
    /// Draws the menu. selectedIndex corresponds to the menu item list ordering.
    /// </summary>
    public void Draw(SpriteBatch sb, int selectedIndex)
    {
        // Background.
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 0, 96));

        // Simple border / backplate.
        sb.Draw(Pixel, new Rectangle(24, 24, 208, 176), new Color(0, 0, 0, 180));
        sb.Draw(Pixel, new Rectangle(26, 26, 204, 172), new Color(20, 40, 120));

        // Header bar.
        sb.Draw(Pixel, new Rectangle(40, 36, 176, 20), new Color(220, 180, 80));
        sb.Draw(Pixel, new Rectangle(44, 40, 168, 12), new Color(40, 40, 60));

        // Menu slots (5 items).
        const int itemCount = 5;
        int startX = 64;
        int startY = 76;
        int itemW = 128;
        int itemH = 16;
        int itemGap = 16;

        for (int i = 0; i < itemCount; i++)
        {
            int y = startY + i * itemGap;
            bool sel = i == selectedIndex;

            var back = sel ? new Color(240, 240, 240) : new Color(0, 0, 0, 80);
            var textBar = sel ? new Color(40, 40, 60) : new Color(160, 160, 180);

            // Selection highlight.
            sb.Draw(Pixel, new Rectangle(startX, y, itemW, itemH), back);

            // Text placeholder bar.
            sb.Draw(Pixel, new Rectangle(startX + 12, y + 5, itemW - 24, 6), textBar);

            // Left marker.
            if (sel)
                sb.Draw(Pixel, new Rectangle(startX - 10, y + 4, 6, 8), new Color(255, 220, 120));
        }

        // NOTE: If/when SpriteFont assets are added, draw actual labels here:
        // PRESEASON, SEASON, PRO BOWL, OPTIONS, DATA.
    }
}
