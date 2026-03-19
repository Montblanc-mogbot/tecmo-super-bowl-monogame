using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Field goal block rush timing scaffold.
///
/// In Tecmo, the FG block team has a short delay before initiating rush,
/// allowing the kick animation to start.
///
/// This component provides a deterministic delay counter.
/// </summary>
public sealed class FieldGoalBlockRushComponent
{
    /// <summary>Frames to wait (60Hz) before rushing.</summary>
    public int DelayFrames { get; set; } = 10;

    /// <summary>Internal frame counter.</summary>
    public int ElapsedFrames { get; set; }

    /// <summary>
    /// Desired rush direction (unit vector in world space).
    /// For a basic FG block, this is typically +/-X.
    /// </summary>
    public Vector2 RushDirection { get; set; } = new Vector2(1, 0);
}
