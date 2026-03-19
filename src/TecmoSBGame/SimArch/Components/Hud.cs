namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Tags an entity as a HUD element and stores the latest bound display values.
///
/// Ported from: ArchiveMge/Components/HudComponent.cs
/// </summary>
public struct Hud
{
    public HudElementType ElementType;

    public bool Visible;

    // Generic bindable fields.
    public int Int0;
    public int Int1;
    public int Int2;

    public string Text0;
    public string Text1;

    public static Hud Default => new()
    {
        ElementType = HudElementType.Scoreboard,
        Visible = true,
        Int0 = 0,
        Int1 = 0,
        Int2 = 0,
        Text0 = string.Empty,
        Text1 = string.Empty,
    };
}

public enum HudElementType
{
    Scoreboard = 0,
    DownDistance = 1,
    PlayClock = 2,
    PossessionIndicator = 3,
}
