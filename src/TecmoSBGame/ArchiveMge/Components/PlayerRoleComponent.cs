namespace TecmoSBGame.Components;

/// <summary>
/// Standardized high-level role for a player entity (QB/RB/WR/etc).
///
/// Note: This is intentionally separate from <see cref="PlayerAttributesComponent.Position"/>
/// (which can store slot strings like WR1/WR2/LG/RT).
/// </summary>
public sealed class PlayerRoleComponent
{
    public PlayerRole Role;

    /// <summary>
    /// Optional formation slot key (e.g. "WR1", "LG"). Useful for debugging.
    /// </summary>
    public string Slot = "";

    public PlayerRoleComponent(PlayerRole role, string slot = "")
    {
        Role = role;
        Slot = slot;
    }
}

public enum PlayerRole
{
    Unknown = 0,

    QB,
    RB,
    WR,
    TE,

    /// <summary>Offensive line.</summary>
    OL,

    /// <summary>Defensive line.</summary>
    DL,

    LB,
    DB,

    K,
    P,
}
