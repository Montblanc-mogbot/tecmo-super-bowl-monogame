namespace TecmoSBGame.SimArch.Components;

public enum RoleId
{
    Unknown = 0,
    QB,
    HB,
    FB,
    WR1,
    WR2,
    TE,
    OC,
    LG,
    RG,
    LT,
    RT,

    // Defense (placeholder ids; expand later)
    DL1,
    DL2,
    DL3,
    DL4,
    LB1,
    LB2,
    LB3,
    LB4,
    CB1,
    CB2,
    S1,
    S2,
}

public struct Role
{
    public RoleId Id;
}
