using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Simple title screen renderer (non-ECS). Uses primitive rectangles only (no fonts required).
/// Virtual resolution: 256x224 (NES).
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
            return tex;
        }
    }

    public void Draw(SpriteBatch sb, float timeSeconds)
    {
        // Background: NES-ish dark blue.
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 0, 128));

        // Stylized "TECMO" block.
        sb.Draw(Pixel, new Rectangle(48, 34, 160, 44), new Color(200, 40, 40));
        sb.Draw(Pixel, new Rectangle(52, 38, 152, 36), new Color(255, 220, 120));

        // "SUPER BOWL" banner.
        sb.Draw(Pixel, new Rectangle(48, 86, 160, 22), new Color(20, 20, 30));
        sb.Draw(Pixel, new Rectangle(52, 90, 152, 14), new Color(220, 220, 220));

        // PRESS START (blink ~1Hz).
        bool showPressStart = ((int)MathF.Floor(timeSeconds * 1f) % 2) == 0;
        if (showPressStart)
        {
            sb.Draw(Pixel, new Rectangle(76, 140, 104, 16), new Color(240, 240, 240));
            sb.Draw(Pixel, new Rectangle(80, 144, 96, 8), new Color(40, 40, 60));
        }

        // Copyright line placeholder.
        sb.Draw(Pixel, new Rectangle(32, 204, 192, 8), new Color(10, 10, 20));
        sb.Draw(Pixel, new Rectangle(36, 206, 184, 4), new Color(180, 180, 180));

        // NOTE: If/when SpriteFont assets are added, replace the rectangles above with real text:
        // "TECMO SUPER BOWL", "PRESS START", and the copyright line.
    }
}
