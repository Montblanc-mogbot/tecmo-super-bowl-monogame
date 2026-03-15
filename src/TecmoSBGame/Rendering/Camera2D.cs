using Microsoft.Xna.Framework;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Simple 2D camera expressed in the game's virtual (256x224) coordinate space.
///
/// We keep this intentionally small and deterministic:
/// - Position is the *top-left* of the virtual view rectangle.
/// - Follow smoothing is expressed as a per-tick gain (0..1) so it behaves stably at 60Hz.
/// </summary>
public sealed class Camera2D
{
    public Vector2 Position;

    public readonly int ViewWidth;
    public readonly int ViewHeight;

    public Camera2D(int viewWidth = 256, int viewHeight = 224)
    {
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        Position = Vector2.Zero;
    }

    public Matrix GetViewMatrix()
    {
        // Translate world into camera space.
        return Matrix.CreateTranslation(new Vector3(-Position, 0f));
    }

    public void ClampToBounds(Rectangle bounds)
    {
        var maxX = bounds.Right - ViewWidth;
        var maxY = bounds.Bottom - ViewHeight;

        var x = MathHelper.Clamp(Position.X, bounds.Left, maxX);
        var y = MathHelper.Clamp(Position.Y, bounds.Top, maxY);
        Position = new Vector2(x, y);
    }

    public void SnapFollow(Vector2 targetCenter)
    {
        var tl = targetCenter - new Vector2(ViewWidth / 2f, ViewHeight / 2f);
        Position = new Vector2(MathF.Round(tl.X), MathF.Round(tl.Y));
    }

    public void SmoothFollow(Vector2 targetCenter, float gainPerTick)
    {
        gainPerTick = MathHelper.Clamp(gainPerTick, 0f, 1f);

        var desired = targetCenter - new Vector2(ViewWidth / 2f, ViewHeight / 2f);
        var next = Vector2.Lerp(Position, desired, gainPerTick);

        // Keep pixel alignment (NES feel).
        Position = new Vector2(MathF.Round(next.X), MathF.Round(next.Y));
    }
}
