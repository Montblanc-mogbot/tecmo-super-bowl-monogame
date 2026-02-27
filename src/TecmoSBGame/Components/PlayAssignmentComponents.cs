using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Identifies which (offensive/defensive) play call an entity is currently executing.
/// Useful for headless inspection and debugging.
/// </summary>
public sealed class PlayCallComponent
{
    public string OffensivePlayName = "";
    public string OffensivePlaySlot = "";
    public string OffensiveFormationId = "";

    public string DefensiveCallId = "";
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
/// </summary>
public sealed class OffensiveAssignmentComponent
{
    public OffensiveAssignmentKind Kind;

    /// <summary>Optional route/track points (for receivers/RB on routes).</summary>
    public readonly List<Vector2> RouteWaypoints = new(capacity: 3);

    /// <summary>
    /// Optional target entity (e.g., primary block assignment).
    /// -1 means none.
    /// </summary>
    public int TargetEntityId = -1;

    public string Notes = "";
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
/// </summary>
public sealed class DefensiveAssignmentComponent
{
    public DefensiveAssignmentKind Kind;

    /// <summary>
    /// Optional target entity to cover/pursue.
    /// -1 means none.
    /// </summary>
    public int TargetEntityId = -1;

    /// <summary>Optional anchor position for zone drops, etc.</summary>
    public Vector2 Anchor;

    public string Notes = "";
}
