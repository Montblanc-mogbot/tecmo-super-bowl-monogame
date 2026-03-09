using System;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering.PlayCall;

public sealed class PlayCallUiAssets : IDisposable
{
    public SpriteFont Font { get; }
    public Texture2D Pixel { get; }

    public PlayCallUiAssets(SpriteFont font, GraphicsDevice graphicsDevice)
    {
        Font = font ?? throw new ArgumentNullException(nameof(font));
        if (graphicsDevice is null) throw new ArgumentNullException(nameof(graphicsDevice));

        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
    }

    public void Dispose()
    {
        Pixel.Dispose();
    }
}
