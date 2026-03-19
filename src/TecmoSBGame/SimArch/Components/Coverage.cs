using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Defensive pass coverage assignment and runtime state (SimArch).
///
/// Intentionally small and frame-based so it can emulate Tecmo-ish timing.
/// </summary>
public struct Coverage
{
    public CoverageType Type;

    /// <summary>Man assignment target entity id. -1 means none.</summary>
    public int AssignmentTargetId;

    public ZoneLandmark Zone;

    // Runtime

    /// <summary>
    /// Cached landmark position in field coordinates.
    /// Deterministically set at play start (or first update) based on formation.
    /// </summary>
    public Vector2 LandmarkPosition;

    /// <summary>
    /// True when the defender is actively pursuing a receiver/ball rather than dropping.
    /// </summary>
    public bool InPursuit;

    /// <summary>Entity id of the current pursuit target (receiver id). -1 means none.</summary>
    public int PursuitTargetId;

    // Reaction timing

    /// <summary>Frames before reacting to a cut/zone threat.</summary>
    public int ReactionDelay;

    /// <summary>Frame counter used for reaction gating.</summary>
    public int ReactionTimer;

    public bool HasReacted;

    public static Coverage Default => new()
    {
        Type = CoverageType.ManToMan,
        AssignmentTargetId = -1,
        Zone = ZoneLandmark.DeepMiddle,
        LandmarkPosition = Vector2.Zero,
        InPursuit = false,
        PursuitTargetId = -1,
        ReactionDelay = 0,
        ReactionTimer = 0,
        HasReacted = false,
    };
}

public enum CoverageType
{
    ManToMan = 0,

    ZoneDeep = 1,
    ZoneFlat = 2,
    ZoneHook = 3,
    ZoneCurl = 4,

    DeepHalf = 5,
    DeepThird = 6,
    DeepQuarter = 7,
}

public enum ZoneLandmark
{
    DeepMiddle = 0,
    DeepLeft = 1,
    DeepRight = 2,

    FlatLeft = 3,
    FlatRight = 4,

    HookLeft = 5,
    HookRight = 6,

    CurlLeft = 7,
    CurlRight = 8,
}
