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
    public float MaxTurnDegreesPerTick;

    // Optional (future): acceleration curves, burst/cut, stamina drain.
    public float AccelPerTick;
    public float DecelPerTick;
}
