namespace TecmoSBGame.SimArch.Components;

public enum MovementActionState
{
    None = 0,
    Burst,
    Dive,
    Cut,
}

/// <summary>
/// Hook points for short-lived movement actions.
///
/// Ported from: ArchiveMge/Components/MovementComponents.cs
/// </summary>
public struct MovementAction
{
    public MovementActionState State;

    /// <summary>Seconds remaining for the current state (when State != None).</summary>
    public float StateTimer;

    /// <summary>Global cooldown for action button usage (seconds).</summary>
    public float CooldownTimer;

    // Default knobs
    public float BurstDurationSeconds;
    public float BurstCooldownSeconds;

    public float DiveDurationSeconds;
    public float DiveCooldownSeconds;

    public float CutDurationSeconds;
    public float CutCooldownSeconds;

    public static MovementAction Default => new()
    {
        State = MovementActionState.None,
        StateTimer = 0f,
        CooldownTimer = 0f,

        BurstDurationSeconds = 0.35f,
        BurstCooldownSeconds = 1.10f,

        DiveDurationSeconds = 0.45f,
        DiveCooldownSeconds = 0.90f,

        CutDurationSeconds = 0.20f,
        CutCooldownSeconds = 0.35f,
    };
}
