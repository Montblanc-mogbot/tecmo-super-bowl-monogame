using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Identifies which (offensive/defensive) play call an entity is currently executing.
///
/// Ported from: ArchiveMge/Components/PlayAssignmentComponents.cs
/// </summary>
public struct PlayCallInfo
{
    public string OffensivePlayName;
    public string OffensivePlaySlot;
    public string OffensiveFormationId;

    public string DefensiveCallId;

    public static PlayCallInfo Default => new()
    {
        OffensivePlayName = string.Empty,
        OffensivePlaySlot = string.Empty,
        OffensiveFormationId = string.Empty,
        DefensiveCallId = string.Empty,
    };
}

public enum OffensiveAssignmentKind
{
    None = 0,
    Quarterback,
    RouteRunner,
    RunCarrier,
    Blocker,
}

/// <summary>
/// High-level offensive assignment. Systems can translate this into behavior states.
///
/// Ported from: ArchiveMge/Components/PlayAssignmentComponents.cs
/// </summary>
public struct OffensiveAssignment
{
    public OffensiveAssignmentKind Kind;

    /// <summary>Optional route/track points (for receivers/RB on routes).</summary>
    public List<Vector2> RouteWaypoints;

    /// <summary>Optional target entity (e.g., primary block assignment). -1 means none.</summary>
    public int TargetEntityId;

    public string Notes;

    public static OffensiveAssignment Default => new()
    {
        Kind = OffensiveAssignmentKind.None,
        RouteWaypoints = new List<Vector2>(capacity: 3),
        TargetEntityId = -1,
        Notes = string.Empty,
    };
}

public enum DefensiveAssignmentKind
{
    None = 0,
    PassRush,
    Pursuit,
    ManCoverage,
    ZoneCoverage,
}

/// <summary>
/// High-level defensive assignment. Placeholder for future defensive AI.
///
/// Ported from: ArchiveMge/Components/PlayAssignmentComponents.cs
/// </summary>
public struct DefensiveAssignment
{
    public DefensiveAssignmentKind Kind;

    /// <summary>Optional target entity to cover/pursue. -1 means none.</summary>
    public int TargetEntityId;

    /// <summary>Optional anchor position for zone drops, etc.</summary>
    public Vector2 Anchor;

    public string Notes;

    public static DefensiveAssignment Default => new()
    {
        Kind = DefensiveAssignmentKind.None,
        TargetEntityId = -1,
        Anchor = Vector2.Zero,
        Notes = string.Empty,
    };
}
