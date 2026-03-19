namespace TecmoSBGame.Components;

/// <summary>
/// Tracks an offensive entity's current blocking objective.
///
/// This is intentionally simple/explicit so headless runs can verify:
/// - which defender was targeted
/// - whether engagement occurred
/// - whether a double-team formed
///
/// Target selection is handled by BlockerAISystem.
/// </summary>
public sealed class BlockTargetComponent
{
    /// <summary>
    /// Entity to block. -1 means no target selected yet.
    /// </summary>
    public int TargetEntityId { get; set; } = -1;

    public BlockAssignmentType Assignment { get; set; }

    public bool IsEngaged { get; set; }

    public int EngagedEntityId { get; set; } = -1;

    /// <summary>
    /// Frame count of engagement (60Hz ticks) since the most recent engagement began.
    /// </summary>
    public int EngagementFrame { get; set; }

    public bool IsDoubleTeam { get; set; }
}

public enum BlockAssignmentType
{
    GapLeft,    // Block left A/B gap
    GapRight,   // Block right A/B gap
    ManOn,      // Block man across
    PullLeft,   // Pull and lead left
    PullRight,  // Pull and lead right
    SecondLevel // Release to LB
}
