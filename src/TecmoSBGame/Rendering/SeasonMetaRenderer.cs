using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Persistence;
using TecmoSBGame.Rendering.UI;

namespace TecmoSBGame.Rendering;

public sealed class SeasonMetaRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _pixel;

    public SeasonMetaRenderer(GraphicsDevice graphicsDevice)
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

    public void Draw(SpriteBatch sb, string title, string body, string footer)
    {
        var small = FontSystem.Instance.GetFont(FontSize.Small);
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(8, 24, 72));
        sb.Draw(Pixel, new Rectangle(8, 8, 240, 208), new Color(0, 0, 0, 180));
        sb.Draw(Pixel, new Rectangle(12, 12, 232, 20), new Color(220, 180, 80));
        sb.Draw(Pixel, new Rectangle(12, 36, 232, 164), new Color(16, 32, 96));
        sb.Draw(Pixel, new Rectangle(12, 204, 232, 12), new Color(0, 0, 0, 90));

        if (small is null)
            return;

        sb.DrawString(small, title, new Vector2(18, 18), Color.White);
        DrawMultiline(sb, small, body, new Vector2(18, 42), 10, Color.White);
        sb.DrawString(small, footer, new Vector2(18, 206), new Color(255, 220, 120));
    }

    private static void DrawMultiline(SpriteBatch sb, SpriteFont font, string text, Vector2 origin, int lineHeight, Color color)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n');
        for (var i = 0; i < lines.Length; i++)
            sb.DrawString(font, lines[i], origin + new Vector2(0, i * lineHeight), color);
    }
}
