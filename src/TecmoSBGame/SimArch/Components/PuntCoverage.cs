using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Punt coverage role (gunner / lane player) scaffold.
///
/// Ported from: ArchiveMge/Components/PuntCoverageComponent.cs
/// </summary>
public struct PuntCoverage
{
    public int LaneIndex;
    public int LaneCount;

    public bool IsGunner;

    public Vector2 LaneLandmark;

    public int ReturnerEntityId;

    public bool BreakOnReturner;

    public static PuntCoverage Default => new()
    {
        LaneIndex = 0,
        LaneCount = 8,
        IsGunner = false,
        LaneLandmark = Vector2.Zero,
        ReturnerEntityId = -1,
        BreakOnReturner = false,
    };
}
