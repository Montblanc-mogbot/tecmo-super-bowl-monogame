namespace TecmoSBGame.Components.PlayCall;

/// <summary>
/// Optional UI helper component for a play list slot.
/// Not currently required for rendering (renderers can draw directly from <see cref="PlayCallComponent"/>),
/// but provided to support ECS-driven UI entities.
/// </summary>
public sealed class PlaySlotComponent
{
    public int Index;
    public string PlayName = "";
    public string Slot = "";

    public bool Selected;
}
