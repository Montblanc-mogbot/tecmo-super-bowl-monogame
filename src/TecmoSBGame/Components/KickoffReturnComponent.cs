using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Kickoff return AI state.
///
/// Deterministic scaffold:
/// - chooses a seam (left/center/right) based on nearby coverage counts
/// - drives Behavior.TargetPosition while kickoff slice is active
///
/// Player control can still override via PlayerControlComponent/MovementInput.
/// </summary>
public sealed class KickoffReturnComponent
{
    public KickoffReturnLane Lane { get; set; } = KickoffReturnLane.Center;

    /// <summary>
    /// Once set, the lane is sticky for a short window so the returner doesn't oscillate.
    /// </summary>
    public int LaneLockFrames { get; set; }

    public Vector2 LastChosenTarget { get; set; }
}

public enum KickoffReturnLane
{
    Left = 0,
    Center = 1,
    Right = 2,
}
