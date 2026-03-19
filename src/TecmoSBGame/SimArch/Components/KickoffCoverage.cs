using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Kickoff coverage lane assignment.
///
/// Ported from: ArchiveMge/Components/KickoffCoverageComponent.cs
/// </summary>
public struct KickoffCoverage
{
    public int LaneIndex;
    public int LaneCount;
    public bool IsContain;
    public Vector2 LaneLandmark;

    public int ReturnerEntityId;
    public bool BreakOnReturner;

    public static KickoffCoverage Default => new()
    {
        LaneIndex = 0,
        LaneCount = 10,
        IsContain = false,
        LaneLandmark = Vector2.Zero,
        ReturnerEntityId = -1,
        BreakOnReturner = false,
    };
}
