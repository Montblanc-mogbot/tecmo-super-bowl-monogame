namespace TecmoSBGame.SimArch.Components.PlayCall;

/// <summary>
/// Optional UI helper component for a formation grid slot.
///
/// Ported from: ArchiveMge/Components/PlayCall/FormationSlotComponent.cs
/// </summary>
public struct FormationSlot
{
    public int Index;
    public int Row;
    public int Col;

    public string FormationId;
    public string DisplayName;

    public bool Selected;

    public static FormationSlot Default => new()
    {
        Index = 0,
        Row = 0,
        Col = 0,
        FormationId = string.Empty,
        DisplayName = string.Empty,
        Selected = false,
    };
}
