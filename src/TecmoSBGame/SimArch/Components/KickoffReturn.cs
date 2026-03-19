using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Kickoff return AI state.
///
/// Ported from: ArchiveMge/Components/KickoffReturnComponent.cs
/// </summary>
public struct KickoffReturn
{
    public KickoffReturnLane Lane;

    /// <summary>
    /// Once set, the lane is sticky for a short window so the returner doesn't oscillate.
    /// </summary>
    public int LaneLockFrames;

    public Vector2 LastChosenTarget;

    public static KickoffReturn Default => new()
    {
        Lane = KickoffReturnLane.Center,
        LaneLockFrames = 0,
        LastChosenTarget = Vector2.Zero,
    };
}

public enum KickoffReturnLane
{
    Left = 0,
    Center = 1,
    Right = 2,
}
