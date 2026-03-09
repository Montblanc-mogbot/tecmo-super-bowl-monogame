using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.UI;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Title screen renderer with text. Virtual resolution: 256x224 (NES).
/// </summary>
public sealed class TitleScreenRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _pixel;

    public TitleScreenRenderer(GraphicsDevice graphicsDevice)
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

    public void Draw(SpriteBatch sb, float timeSeconds)
    {
        var font = FontSystem.Instance.GetFont(FontSize.Large);
        var mediumFont = FontSystem.Instance.GetFont(FontSize.Medium);
        var smallFont = FontSystem.Instance.GetFont(FontSize.Small);

        // Background: NES-ish dark blue.
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 0, 128));

        // "TECMO" block background.
        sb.Draw(Pixel, new Rectangle(48, 34, 160, 44), new Color(200, 40, 40));
        sb.Draw(Pixel, new Rectangle(52, 38, 152, 36), new Color(255, 220, 120));

        // TECMO text (using SpriteFont if available, else fallback to NesHudFont via FontSystem)
        if (font != null)
        {
            var titleText = "TECMO";
            var titleSize = font.MeasureString(titleText);
            sb.DrawString(font, titleText, new Vector2(128 - titleSize.X / 2, 44), new Color(200, 40, 40));
        }

        // "SUPER BOWL" banner.
        sb.Draw(Pixel, new Rectangle(48, 86, 160, 22), new Color(20, 20, 30));
        sb.Draw(Pixel, new Rectangle(52, 90, 152, 14), new Color(220, 220, 220));

        if (mediumFont != null)
        {
            var subtitleText = "SUPER BOWL";
            var subtitleSize = mediumFont.MeasureString(subtitleText);
            sb.DrawString(mediumFont, subtitleText, new Vector2(128 - subtitleSize.X / 2, 90), Color.Black);
        }

        // PRESS START (blink ~1Hz).
        bool showPressStart = ((int)MathF.Floor(timeSeconds * 1f) % 2) == 0;
        if (showPressStart)
        {
            sb.Draw(Pixel, new Rectangle(76, 140, 104, 16), new Color(240, 240, 240));
            sb.Draw(Pixel, new Rectangle(80, 144, 96, 8), new Color(40, 40, 60));

            if (smallFont != null)
            {
                var promptText = "PRESS START";
                var promptSize = smallFont.MeasureString(promptText);
                sb.DrawString(smallFont, promptText, new Vector2(128 - promptSize.X / 2, 143), Color.White);
            }
        }

        // Copyright line.
        sb.Draw(Pixel, new Rectangle(32, 204, 192, 8), new Color(10, 10, 20));
        sb.Draw(Pixel, new Rectangle(36, 206, 184, 4), new Color(180, 180, 180));

        if (smallFont != null)
        {
            var copyText = "© 1991 TECMO";
            var copySize = smallFont.MeasureString(copyText);
            sb.DrawString(smallFont, copyText, new Vector2(128 - copySize.X / 2, 204), new Color(180, 180, 180));
        }
    }
}
