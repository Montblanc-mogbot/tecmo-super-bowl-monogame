namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Movement tuning values for SimArch.
///
/// Convention:
/// - Values are expressed per 60Hz tick (units/tick, degrees/tick, etc.).
/// - Systems may scale by (dtSeconds * 60) when integrating.
/// </summary>
public struct MovementTuning
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
    public bool UseAccelCurve;

    /// <summary>
    /// Maximum turning rate (degrees per 60Hz tick) when applying a new desired direction.
    /// Tecmo feel comes largely from limiting how quickly a player can rotate.
    /// </summary>
    public float MaxTurnDegreesPerTick;

    public static MovementTuning Create(
        float maxSpeedPerTick,
        float accelPerTick,
        float decelPerTick,
        float cutPenalty,
        float burstMultiplier)
        => new()
        {
            MaxSpeedPerTick = maxSpeedPerTick,
            AccelPerTick = accelPerTick,
            DecelPerTick = decelPerTick,
            CutPenalty = cutPenalty,
            BurstMultiplier = burstMultiplier,
            UseAccelCurve = true,
            MaxTurnDegreesPerTick = 9f,
        };
}
