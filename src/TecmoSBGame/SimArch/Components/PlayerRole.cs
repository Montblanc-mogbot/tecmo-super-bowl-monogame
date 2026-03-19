namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Standardized high-level role for a player entity (QB/RB/WR/etc).
///
/// NOTE: SimArch primarily uses <see cref="Role"/> + <see cref="RoleId"/>.
/// This component is provided for parity with the legacy MGE model.
///
/// Ported from: ArchiveMge/Components/PlayerRoleComponent.cs
/// </summary>
public struct PlayerRole
{
    public PlayerRoleKind Role;

    /// <summary>
    /// Optional formation slot key (e.g. "WR1", "LG"). Useful for debugging.
    /// </summary>
    public string Slot;

    public static PlayerRole Create(PlayerRoleKind role, string slot = "") => new()
    {
        Role = role,
        Slot = slot,
    };
}

public enum PlayerRoleKind
{
    Unknown = 0,
    QB,
    RB,
    WR,
    TE,
    OL,
    DL,
    LB,
    DB,
    K,
    P,
}
