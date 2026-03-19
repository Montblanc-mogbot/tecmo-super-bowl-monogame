using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Marker/tag component for the camera entity.
///
/// Ported from: ArchiveMge/Components/CameraComponents.cs
/// </summary>
public struct CameraTag
{
}

/// <summary>
/// Config for Tecmo-like field scrolling.
///
/// The follow model is intentionally assembly-ish:
/// - FollowGainPerTick: how aggressively we move towards the target each 60Hz tick
/// - Deadzone: a rectangle (in view space) within which the target is allowed to move
///   without scrolling.
///
/// Ported from: ArchiveMge/Components/CameraComponents.cs
/// </summary>
public struct CameraFollowConfig
{
    public float FollowGainPerTick;

    // Deadzone expressed relative to the view top-left.
    public Rectangle Deadzone;

    public static CameraFollowConfig Default => new()
    {
        FollowGainPerTick = 0.20f,
        Deadzone = new Rectangle(
            x: 96,
            y: 84,
            width: 64,
            height: 56),
    };
}

/// <summary>
/// A camera follow target. If multiple exist, camera systems can pick one by priority.
///
/// Ported from: ArchiveMge/Components/CameraComponents.cs
/// </summary>
public struct CameraTarget
{
    public int Priority;
}
