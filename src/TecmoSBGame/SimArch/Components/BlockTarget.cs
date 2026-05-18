namespace TecmoSBGame.SimArch.Components;

// Ported from: ArchiveMge/Components/BlockTargetComponent.cs

/// <summary>
/// Tracks an offensive entity's current blocking objective (SimArch).
/// </summary>
public struct BlockTarget
{
    /// <summary>Entity to block. -1 means no target selected yet.</summary>
    public int TargetEntityId;

    public BlockAssignmentType Assignment;

    /// <summary>
    /// Optional Tecmo/disassembly-inspired defender hint (e.g. RE, NT, ROLB).
    /// Used to keep initial assignments tied to authored play/formation intent.
    /// </summary>
    public string PreferredDefenderKey;

    public bool IsEngaged;
    public int EngagedEntityId;

    /// <summary>Frame count of engagement (60Hz ticks) since the most recent engagement began.</summary>
    public int EngagementFrame;

    public bool IsDoubleTeam;
    public int PressureContributionFrames;
    public int AssignmentStickFrames;
    public int FailedEngagements;
    public float LastTargetDistanceSq;

    public static BlockTarget Default => new()
    {
        TargetEntityId = -1,
        Assignment = BlockAssignmentType.ManOn,
        PreferredDefenderKey = string.Empty,
        IsEngaged = false,
        EngagedEntityId = -1,
        EngagementFrame = 0,
        IsDoubleTeam = false,
        PressureContributionFrames = 0,
        AssignmentStickFrames = 0,
        FailedEngagements = 0,
        LastTargetDistanceSq = float.PositiveInfinity,
    };
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
