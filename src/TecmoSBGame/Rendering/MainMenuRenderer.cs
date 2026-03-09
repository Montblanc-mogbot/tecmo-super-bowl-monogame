using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Main menu renderer with text. Virtual resolution: 256x224 (NES).
/// </summary>
public sealed class MainMenuRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _pixel;

    private static readonly string[] MenuItems = new[]
    {
        "PRESEASON",
        "SEASON",
        "PRO BOWL",
        "OPTIONS",
        "DATA"
    };

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
            return _pixel;
        }
    }

    /// <summary>
    /// Draws the menu. selectedIndex corresponds to the menu item list ordering.
    /// </summary>
    public void Draw(SpriteBatch sb, int selectedIndex)
    {
        var font = FontSystem.Instance.GetFont(FontSize.Medium);
        var smallFont = FontSystem.Instance.GetFont(FontSize.Small);

        // Background.
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 0, 96));

        // Simple border / backplate.
        sb.Draw(Pixel, new Rectangle(24, 24, 208, 176), new Color(0, 0, 0, 180));
        sb.Draw(Pixel, new Rectangle(26, 26, 204, 172), new Color(20, 40, 120));

        // Header bar.
        sb.Draw(Pixel, new Rectangle(40, 36, 176, 20), new Color(220, 180, 80));
        sb.Draw(Pixel, new Rectangle(44, 40, 168, 12), new Color(40, 40, 60));

        // Header text.
        if (smallFont != null)
        {
            var headerText = "MAIN MENU";
            var headerSize = smallFont.MeasureString(headerText);
            sb.DrawString(smallFont, headerText, new Vector2(128 - headerSize.X / 2, 40), Color.White);
        }

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
            var textColor = sel ? new Color(40, 40, 60) : new Color(220, 220, 220);

            // Selection highlight background.
            sb.Draw(Pixel, new Rectangle(startX, y, itemW, itemH), back);

            // Menu item text.
            if (font != null)
            {
                var itemText = MenuItems[i];
                var itemSize = font.MeasureString(itemText);
                float textX = startX + (itemW - itemSize.X) / 2;
                float textY = y + (itemH - itemSize.Y) / 2;
                sb.DrawString(font, itemText, new Vector2(textX, textY), textColor);
            }

            // Left marker arrow for selected item.
            if (sel)
                sb.Draw(Pixel, new Rectangle(startX - 10, y + 4, 6, 8), new Color(255, 220, 120));
        }

        // Footer hint.
        if (smallFont != null)
        {
            var hintText = "USE ARROWS + ENTER";
            var hintSize = smallFont.MeasureString(hintText);
            sb.DrawString(smallFont, hintText, new Vector2(128 - hintSize.X / 2, 200), new Color(160, 160, 160));
        }
    }
}
