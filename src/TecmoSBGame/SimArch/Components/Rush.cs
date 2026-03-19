using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

// Existing SimArch rush assignment used by DefensiveRushSystem.
public enum RushAssignment
{
    AGapLeft = 0,
    AGapRight = 1,
    BGapLeft = 2,
    BGapRight = 3,
    EdgeLeft = 4,
    EdgeRight = 5,
}

/// <summary>
/// Ported from: ArchiveMge/Components/RushComponent.cs
/// </summary>
public enum RushGap
{
    ALeft,
    BLeft,
    CLeft,

    ARight,
    BRight,
    CRight,

    ContainLeft,
    ContainRight,
}

/// <summary>
/// Ported from: ArchiveMge/Components/RushComponent.cs
/// </summary>
public enum RushType
{
    Power,
    Swim,
    Spin,
    Bull,
}

/// <summary>
/// Pass rush assignment + runtime state.
///
/// NOTE: Current SimArch systems still use <see cref="RushAssignment"/> + Landmark fields.
/// The additional fields below are added for parity with the legacy MGE model.
/// </summary>
public struct Rush
{
    // ---- Current SimArch fields ----
    public RushAssignment Assignment;
    public bool HasLandmark;
    public Vector2 Landmark;
    public bool ReachedLandmark;

    // ---- Legacy/MGE parity fields ----

    public RushGap TargetGap;
    public RushType Type;
    public bool IsContain;

    public bool IsStunt;
    public int StuntDelayFrames;
    public RushGap StuntTargetGap;

    public bool GapReached;
    public Vector2 GapPosition;

    public bool Engaged;
    public int EngagedBlockerId;

    public int LastRushMoveFrame;

    public const int RUSH_MOVE_COOLDOWN = 30;

    public static Rush Default => new()
    {
        Assignment = RushAssignment.AGapLeft,
        HasLandmark = false,
        Landmark = Vector2.Zero,
        ReachedLandmark = false,

        TargetGap = RushGap.ALeft,
        Type = RushType.Power,
        IsContain = false,

        IsStunt = false,
        StuntDelayFrames = 0,
        StuntTargetGap = RushGap.ALeft,

        GapReached = false,
        GapPosition = Vector2.Zero,

        Engaged = false,
        EngagedBlockerId = -1,

        LastRushMoveFrame = -60,
    };
}
