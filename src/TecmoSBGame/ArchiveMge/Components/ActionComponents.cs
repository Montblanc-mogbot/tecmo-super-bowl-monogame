namespace TecmoSBGame.Components;

/// <summary>
/// High-level command intent produced by input (or AI) and resolved into gameplay.
/// This is intentionally animation-agnostic.
/// </summary>
public enum PlayerActionCommand
{
    None = 0,

    // Defense
    Tackle,

    // Universal movement actions
    Dive,
    SprintBurst,
    JukeCut,

    // Offense
    Snap,
    Pass,
    Pitch,
    Scramble,
}

/// <summary>
/// Per-entity action request/state.
/// Input writes a pending command; resolution systems consume it.
/// Also stores a small history for deterministic headless snapshots/logging.
/// </summary>
public sealed class PlayerActionStateComponent
{
    public PlayerActionCommand PendingCommand = PlayerActionCommand.None;
    public int? PendingTargetEntityId = null;

    public PlayerActionCommand LastAppliedCommand = PlayerActionCommand.None;
    public int? LastAppliedTargetEntityId = null;

    // Minimal per-entity input edge tracking (kept here so InputSystem remains stateless).
    public bool PrevActionDown;
    public bool PrevPitchDown;
    public bool PrevSprintDown;
    public bool PrevJukeDown;
}
