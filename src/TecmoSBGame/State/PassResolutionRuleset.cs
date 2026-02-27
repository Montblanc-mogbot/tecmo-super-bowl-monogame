namespace TecmoSBGame.State;

/// <summary>
/// Flag for how pass completion should be resolved.
///
/// We currently use a clean-room approximation. The long-term goal is an assembly-parity
/// implementation (Tecmo Super Bowl NES) once we extract the exact tables/thresholds.
/// </summary>
public enum PassResolutionRuleset
{
    /// <summary>
    /// Clean-room, deterministic approximation (rating + proximity + deterministic RNG).
    /// </summary>
    CleanRoomApprox = 0,

    /// <summary>
    /// Intended future mode: match original Tecmo Super Bowl logic and tables.
    /// </summary>
    AssemblyParity = 1,
}
