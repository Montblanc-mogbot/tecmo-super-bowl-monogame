namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Temporary speed multiplier applied on top of an entity's normal movement tuning.
/// Used for tackle outcomes like stumble.
/// Deterministic: purely time-based, decays by fixed dt.
///
/// Ported from: ArchiveMge/Components/SpeedModifierComponent.cs
/// </summary>
public struct SpeedModifier
{
    /// <summary>Multiplicative factor applied to max speed (e.g. 0.65 = 35% slower).</summary>
    public float MaxSpeedMultiplier;

    /// <summary>Seconds remaining for this modifier. When &lt;= 0, modifier is treated as inactive.</summary>
    public float TimerSeconds;

    public static SpeedModifier Default => new() { MaxSpeedMultiplier = 1.0f, TimerSeconds = 0.0f };
}
