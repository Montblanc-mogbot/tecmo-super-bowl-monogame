using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Pass rush assignment + runtime state.
///
/// Tecmo-style rush is gap-assignment based:
/// - Phase 1: attack assigned gap/contain landmark
/// - Phase 2: rush QB from that lane
/// - If engaged with a blocker, periodically attempt a rush move to disengage
///
/// Determinism note:
/// Systems should use PlayState.PlayElapsedSeconds (60Hz) to derive a frame index and
/// deterministic hash-based RNG (playId + entity ids + frame) when rolling rush moves.
/// </summary>
public sealed class RushComponent
{
    // ---- Assignment ----

    public RushGap TargetGap { get; set; }
    public RushType Type { get; set; }
    public bool IsContain { get; set; }

    // ---- Stunt / twist ----

    public bool IsStunt { get; set; }

    /// <summary>
    /// Frame index (60Hz) at which the stunt swaps the rusher's gap assignment.
    /// </summary>
    public int StuntDelayFrames { get; set; }

    public RushGap StuntTargetGap { get; set; }

    // ---- Runtime state ----

    public bool GapReached { get; set; }
    public Vector2 GapPosition { get; set; }

    public bool Engaged { get; set; }
    public int EngagedBlockerId { get; set; } = -1;

    // ---- Rush move cooldown (frame-based) ----

    /// <summary>
    /// Last frame index when this rusher attempted a rush move.
    /// Initialize negative so the rusher can attempt immediately.
    /// </summary>
    public int LastRushMoveFrame { get; set; } = -60;

    /// <summary>
    /// Frames between rush move attempts.
    /// </summary>
    public const int RUSH_MOVE_COOLDOWN = 30;
}

public enum RushGap
{
    ALeft,   // Between C and LG
    BLeft,   // Between LG and LT
    CLeft,   // Outside LT

    ARight,  // Between C and RG
    BRight,  // Between RG and RT
    CRight,  // Outside RT

    ContainLeft,
    ContainRight,
}

public enum RushType
{
    Power,  // HP-based
    Swim,   // MS-based
    Spin,   // Rare, MS-based
    Bull,   // HP-based, slower
}
