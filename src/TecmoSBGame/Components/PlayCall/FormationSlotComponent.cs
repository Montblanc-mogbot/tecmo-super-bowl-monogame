namespace TecmoSBGame.Components.PlayCall;

/// <summary>
/// Optional UI helper component for a formation grid slot.
/// Not currently required for rendering (renderers can draw directly from <see cref="PlayCallComponent"/>),
/// but provided to support ECS-driven UI entities.
/// </summary>
public sealed class FormationSlotComponent
{
    public int Index;
    public int Row;
    public int Col;

    public string FormationId = "";
    public string DisplayName = "";

    public bool Selected;
}
