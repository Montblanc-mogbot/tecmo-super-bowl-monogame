using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Manages the rendering viewport and scaling.
/// NES resolution: 256x224, scaled to fit window while maintaining aspect ratio.
/// </summary>
public sealed class RenderViewport
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly int _virtualWidth = 256;
    private readonly int _virtualHeight = 224;
    
    public Matrix ScaleMatrix { get; private set; }
    public Rectangle DestinationRect { get; private set; }
    public float Scale { get; private set; }
    
    public RenderViewport(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        UpdateScale();
    }
    
    /// <summary>
    /// Updates the scale matrix when window size changes.
    /// </summary>
    public void UpdateScale()
    {
        var viewport = _graphicsDevice.Viewport;
        
        // Calculate scale to fit while maintaining aspect ratio
        float scaleX = viewport.Width / (float)_virtualWidth;
        float scaleY = viewport.Height / (float)_virtualHeight;
        Scale = MathF.Min(scaleX, scaleY);
        
        // Calculate centered destination rectangle
        int destWidth = (int)(_virtualWidth * Scale);
        int destHeight = (int)(_virtualHeight * Scale);
        int destX = (viewport.Width - destWidth) / 2;
        int destY = (viewport.Height - destHeight) / 2;
        
        DestinationRect = new Rectangle(destX, destY, destWidth, destHeight);
        
        // Create scale matrix
        ScaleMatrix = Matrix.CreateScale(Scale);
    }
    
    /// <summary>
    /// Transforms screen coordinates to virtual game coordinates.
    /// </summary>
    public Vector2 ScreenToVirtual(Vector2 screenPos)
    {
        return new Vector2(
            (screenPos.X - DestinationRect.X) / Scale,
            (screenPos.Y - DestinationRect.Y) / Scale);
    }
    
    /// <summary>
    /// Transforms virtual game coordinates to screen coordinates.
    /// </summary>
    public Vector2 VirtualToScreen(Vector2 virtualPos)
    {
        return new Vector2(
            virtualPos.X * Scale + DestinationRect.X,
            virtualPos.Y * Scale + DestinationRect.Y);
    }
}
