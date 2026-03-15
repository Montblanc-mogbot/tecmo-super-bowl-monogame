using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Marker/tag component for the camera entity.
/// </summary>
public sealed class CameraComponent
{
}

/// <summary>
/// Config for Tecmo-like field scrolling.
///
/// The follow model is intentionally assembly-ish:
/// - followGainPerTick: how aggressively we move towards the target each 60Hz tick
/// - deadzone: a rectangle (in view space) within which the target is allowed to move
///   without scrolling.
/// </summary>
public sealed class CameraFollowConfigComponent
{
    public float FollowGainPerTick = 0.20f;

    // Deadzone expressed relative to the view top-left.
    public Rectangle Deadzone = new Rectangle(
        x: 96,
        y: 84,
        width: 64,
        height: 56);
}

/// <summary>
/// A camera follow target. If multiple exist, CameraSystem picks one by priority.
/// </summary>
public sealed class CameraTargetComponent
{
    public int Priority;

    public CameraTargetComponent(int priority)
    {
        Priority = priority;
    }
}
