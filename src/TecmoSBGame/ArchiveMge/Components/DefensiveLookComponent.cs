namespace TecmoSBGame.Components;

/// <summary>
/// Lightweight per-play hint for offensive AI about the defensive coverage look.
///
/// This is a scaffold: it is derived from the defensive assignments chosen for the play
/// (man vs zone) and attached to offensive entities so systems like RouteFollowSystem
/// can apply deterministic adjustments without needing to scan the whole world.
/// </summary>
public sealed class DefensiveLookComponent
{
    public bool IsMan;
    public bool IsZone;
}
