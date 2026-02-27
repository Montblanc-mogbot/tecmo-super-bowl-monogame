using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Per-entity movement tuning knobs (intended to be data/YAML driven later).
///
/// Notes:
/// - Speeds are in "units per 60Hz tick" (not units/sec), matching the existing codebase.
/// - Accel/Decel are "speed units per tick" at 60Hz.
/// </summary>
public sealed class MovementTuningComponent
{
    public float MaxSpeedPerTick;

    /// <summary>
    /// How quickly speed ramps up towards MaxSpeedPerTick.
    /// </summary>
    public float AccelPerTick;

    /// <summary>
    /// How quickly speed is removed when no movement direction is desired.
    /// Higher values approach "instant stop".
    /// </summary>
    public float DecelPerTick;

    /// <summary>
    /// Fraction of speed removed on a sharp direction change.
    /// 0 = no penalty, 1 = full stop.
    /// </summary>
    public float CutPenalty;

    /// <summary>
    /// Multiplicative speed boost when in a Burst action.
    /// </summary>
    public float BurstMultiplier;

    /// <summary>
    /// If true, speed ramps with a Tecmo-like curve (fast early, taper near max).
    /// </summary>
    public bool UseAccelCurve = true;

    public MovementTuningComponent(
        float maxSpeedPerTick,
        float accelPerTick,
        float decelPerTick,
        float cutPenalty,
        float burstMultiplier)
    {
        MaxSpeedPerTick = maxSpeedPerTick;
        AccelPerTick = accelPerTick;
        DecelPerTick = decelPerTick;
        CutPenalty = cutPenalty;
        BurstMultiplier = burstMultiplier;
    }
}

/// <summary>
/// Movement input intent (only meaningful for the single controlled entity).
/// AI should typically drive <see cref="BehaviorComponent"/> instead.
/// </summary>
public sealed class MovementInputComponent
{
    /// <summary>
    /// Normalized desired direction.
    /// </summary>
    public Vector2 Direction;
}

public enum MovementActionState
{
    None = 0,
    Burst,
    Dive,
    Cut
}

/// <summary>
/// Hook points for short-lived movement actions.
/// (No animation required yet; just timers/cooldowns and a current state.)
/// </summary>
public sealed class MovementActionComponent
{
    public MovementActionState State;

    /// <summary>
    /// Seconds remaining for the current state (when State != None).
    /// </summary>
    public float StateTimer;

    /// <summary>
    /// Global cooldown for action button usage (seconds).
    /// </summary>
    public float CooldownTimer;

    // Default knobs (can be moved into YAML/tuning later)
    public float BurstDurationSeconds = 0.35f;
    public float BurstCooldownSeconds = 1.10f;

    public float DiveDurationSeconds = 0.45f;
    public float DiveCooldownSeconds = 0.90f;

    public float CutDurationSeconds = 0.20f;
    public float CutCooldownSeconds = 0.35f;
}
