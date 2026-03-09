using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Defensive pass coverage assignment and runtime state.
///
/// This is intentionally small and frame-based so it can emulate Tecmo's
/// assignment tables while remaining deterministic.
/// </summary>
public sealed class CoverageComponent
{
    public CoverageType Type { get; set; }

    /// <summary>
    /// Entity id of the man-coverage assignment target (typically an eligible receiver).
    /// -1 means none.
    /// </summary>
    public int AssignmentTargetId { get; set; } = -1;

    /// <summary>
    /// Zone landmark kind. Only used when <see cref="Type"/> is zone-based.
    /// </summary>
    public ZoneLandmark Zone { get; set; }

    // Runtime state

    /// <summary>
    /// Cached landmark position in field coordinates.
    /// Deterministically set at play start (or first update) based on formation.
    /// </summary>
    public Vector2 LandmarkPosition { get; set; }

    /// <summary>
    /// True when the defender is actively pursuing a receiver/ball rather than dropping.
    /// </summary>
    public bool InPursuit { get; set; }

    /// <summary>
    /// Entity id of the current pursuit target (receiver id). -1 means none.
    /// </summary>
    public int PursuitTargetId { get; set; } = -1;

    // Assembly-accurate timing

    /// <summary>
    /// Frames before reacting to a cut/zone threat.
    /// </summary>
    public int ReactionDelay { get; set; }

    /// <summary>
    /// Frame counter used for reaction gating.
    /// </summary>
    public int ReactionTimer { get; set; }

    public bool HasReacted { get; set; }
}

public enum CoverageType
{
    ManToMan,

    // Generic zones
    ZoneDeep,
    ZoneFlat,
    ZoneHook,
    ZoneCurl,

    // Deep safety responsibilities
    DeepHalf,    // Cover 2 safety
    DeepThird,   // Cover 3 safety
    DeepQuarter  // Cover 4 safety
}

public enum ZoneLandmark
{
    DeepMiddle,
    DeepLeft,
    DeepRight,

    FlatLeft,
    FlatRight,

    HookLeft,
    HookRight,

    CurlLeft,
    CurlRight
}
