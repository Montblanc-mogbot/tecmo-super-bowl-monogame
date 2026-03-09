using System;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering.UI;

/// <summary>
/// UI scaling helper.
///
/// The game renders in a 256x224 virtual resolution (NES). The SpriteBatch in MainGame
/// already uses a point-sampled scale matrix from RenderViewport; UI can generally be
/// authored in virtual pixels.
///
/// This helper exists as a single source of truth for the base virtual resolution and
/// can be used by future screens that need to know the integer scale factor.
/// </summary>
public static class UiScaling
{
    public const int BaseWidth = 256;
    public const int BaseHeight = 224;

    /// <summary>
    /// Returns the integer scale factor that fits the base resolution into the current backbuffer.
    /// This is useful for maintaining a crisp pixel aesthetic.
    /// </summary>
    public static int GetIntegerScale(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice is null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        var vp = graphicsDevice.Viewport;
        var sx = Math.Max(1, vp.Width / BaseWidth);
        var sy = Math.Max(1, vp.Height / BaseHeight);
        return Math.Max(1, Math.Min(sx, sy));
    }
}
