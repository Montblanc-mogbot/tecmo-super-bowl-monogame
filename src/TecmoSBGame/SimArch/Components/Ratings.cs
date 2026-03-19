namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Core player ratings used by Tecmo-ish systems.
///
/// Keep this intentionally small and deterministic.
/// Ratings are normalized to 0..100 where practical.
/// </summary>
public struct Ratings
{
    /// <summary>Max speed (Tecmo MS). Commonly treated as 0..69 in classic formulas.</summary>
    public int MS;

    /// <summary>Hit power (Tecmo HP).</summary>
    public int HP;

    /// <summary>Rushing speed / agility (Tecmo RS).</summary>
    public int RS;
}
