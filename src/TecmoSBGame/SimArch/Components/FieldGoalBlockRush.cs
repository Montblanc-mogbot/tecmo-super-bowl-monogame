using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Field goal block rush timing scaffold.
///
/// Ported from: ArchiveMge/Components/FieldGoalBlockRushComponent.cs
/// </summary>
public struct FieldGoalBlockRush
{
    /// <summary>Frames to wait (60Hz) before rushing.</summary>
    public int DelayFrames;

    /// <summary>Internal frame counter.</summary>
    public int ElapsedFrames;

    /// <summary>
    /// Desired rush direction (unit vector in world space).
    /// For a basic FG block, this is typically +/-X.
    /// </summary>
    public Vector2 RushDirection;

    public static FieldGoalBlockRush Default => new()
    {
        DelayFrames = 10,
        ElapsedFrames = 0,
        RushDirection = new Vector2(1, 0),
    };
}
