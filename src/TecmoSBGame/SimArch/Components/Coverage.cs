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
    public Vector2 LandmarkPosition;
    public bool InPursuit;
    public int PursuitTargetId;

    // Reaction timing
    public int ReactionDelay;
    public int ReactionTimer;
    public bool HasReacted;
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
