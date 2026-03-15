using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Punt coverage role (gunner / lane player) scaffold.
///
/// Similar to kickoff coverage, but keys off Punt ball flight.
/// </summary>
public sealed class PuntCoverageComponent
{
    public int LaneIndex { get; set; }
    public int LaneCount { get; set; } = 8;

    public bool IsGunner { get; set; }

    public Vector2 LaneLandmark { get; set; }

    public int ReturnerEntityId { get; set; } = -1;

    public bool BreakOnReturner { get; set; }
}
