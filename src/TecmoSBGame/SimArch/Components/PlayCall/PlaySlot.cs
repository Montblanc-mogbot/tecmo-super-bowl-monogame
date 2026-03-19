namespace TecmoSBGame.SimArch.Components.PlayCall;

/// <summary>
/// Optional UI helper component for a play list slot.
///
/// Ported from: ArchiveMge/Components/PlayCall/PlaySlotComponent.cs
/// </summary>
public struct PlaySlot
{
    public int Index;
    public string PlayName;
    public string Slot;
    public bool Selected;

    public static PlaySlot Default => new()
    {
        Index = 0,
        PlayName = string.Empty,
        Slot = string.Empty,
        Selected = false,
    };
}
