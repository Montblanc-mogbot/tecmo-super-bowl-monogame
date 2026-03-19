namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Tracks an offensive entity's current blocking objective (SimArch).
/// </summary>
public struct BlockTarget
{
    /// <summary>Entity to block. -1 means no target selected yet.</summary>
    public int TargetEntityId;

    public BlockAssignmentType Assignment;

    public bool IsEngaged;
    public int EngagedEntityId;

    /// <summary>Frame count of engagement (60Hz ticks) since the most recent engagement began.</summary>
    public int EngagementFrame;

    public bool IsDoubleTeam;
}

public enum BlockAssignmentType
{
    GapLeft,
    GapRight,
    ManOn,
    PullLeft,
    PullRight,
    SecondLevel,
}
