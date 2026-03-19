using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Punt return blocking/laning scaffold.
///
/// This mirrors kickoff return lane selection, but is intended for punt scenarios
/// (slower developing, wall setup).
/// </summary>
public sealed class PuntReturnComponent
{
    public PuntReturnLane Lane { get; set; } = PuntReturnLane.Center;
    public int LaneLockFrames { get; set; }
    public Vector2 LastChosenTarget { get; set; }
}

public enum PuntReturnLane
{
    Left = 0,
    Center = 1,
    Right = 2,
}
