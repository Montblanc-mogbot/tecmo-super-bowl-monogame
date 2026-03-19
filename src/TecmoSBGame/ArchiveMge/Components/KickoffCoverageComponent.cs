using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Kickoff coverage lane assignment.
///
/// Tecmo-style kickoff teams behave like lane-assigned pursuers with contain rules.
/// This component is a light scaffold so kickoff slice/systems can remain deterministic.
/// </summary>
public sealed class KickoffCoverageComponent
{
    /// <summary>
    /// Logical lane index from left->right (relative to offense direction).
    /// Purely descriptive right now; systems may map it to target landmarks.
    /// </summary>
    public int LaneIndex { get; set; }

    /// <summary>
    /// Total lane count (for normalization / landmark spacing).
    /// </summary>
    public int LaneCount { get; set; } = 10;

    /// <summary>
    /// If true, try to keep outside leverage and avoid over-pursuit.
    /// </summary>
    public bool IsContain { get; set; }

    /// <summary>
    /// Landmark in field coordinates that this coverage player should attack before
    /// transitioning to the returner/ball.
    /// </summary>
    public Vector2 LaneLandmark { get; set; }

    /// <summary>
    /// Entity id for the expected returner.
    /// Spawners should set this deterministically for kickoff scenarios.
    /// </summary>
    public int ReturnerEntityId { get; set; } = -1;

    /// <summary>
    /// When true, coverage breaks directly on the returner (ball carrier) rather than
    /// honoring lane landmark. Useful after the returner commits to a side.
    /// </summary>
    public bool BreakOnReturner { get; set; }
}
