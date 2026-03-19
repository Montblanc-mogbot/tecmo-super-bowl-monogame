namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Frame-level UI/button edges provided by the host (MainGameArch).
///
/// This is intentionally not an ECS component; it is stored on Sim and consumed by systems.
/// </summary>
public struct UiButtons
{
    public bool Up;
    public bool Down;
    public bool Left;
    public bool Right;

    public bool Select;   // confirm / A
    public bool Back;     // cancel / B

    public bool Snap;     // snap ball (can be same as Select depending on context)
    public bool Continue; // advance from post-play
}
