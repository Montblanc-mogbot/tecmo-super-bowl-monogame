using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Punt return blocking/laning scaffold.
///
/// Ported from: ArchiveMge/Components/PuntReturnComponent.cs
/// </summary>
public struct PuntReturn
{
    public PuntReturnLane Lane;
    public int LaneLockFrames;
    public Vector2 LastChosenTarget;

    public static PuntReturn Default => new()
    {
        Lane = PuntReturnLane.Center,
        LaneLockFrames = 0,
        LastChosenTarget = Vector2.Zero,
    };
}

public enum PuntReturnLane
{
    Left = 0,
    Center = 1,
    Right = 2,
}
