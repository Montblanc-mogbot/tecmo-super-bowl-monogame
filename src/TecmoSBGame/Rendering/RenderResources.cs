using System;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Owns and caches GPU resources used by renderers.
///
/// Principle: never allocate GPU resources inside Draw() paths.
/// </summary>
public sealed class RenderResources : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;

    public RenderResources(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        Pixel = new Texture2D(_graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
    }

    /// <summary>
    /// A 1x1 white pixel texture for drawing solid rectangles/lines via tinting.
    /// </summary>
    public Texture2D Pixel { get; }

    public void Dispose()
    {
        Pixel.Dispose();
    }
}
