namespace TecmoSBGame.Components;

/// <summary>
/// Temporary speed multiplier applied on top of an entity's normal movement tuning.
/// Used for tackle outcomes like stumble.
/// Deterministic: purely time-based, decays by fixed dt.
/// </summary>
public sealed class SpeedModifierComponent
{
    /// <summary>
    /// Multiplicative factor applied to max speed (e.g. 0.65 = 35% slower).
    /// </summary>
    public float MaxSpeedMultiplier = 1.0f;

    /// <summary>
    /// Seconds remaining for this modifier. When <= 0, modifier is treated as inactive.
    /// </summary>
    public float TimerSeconds = 0.0f;
}
