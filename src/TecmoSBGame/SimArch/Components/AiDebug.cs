using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Global toggle + options for AI debug visualization.
/// Attach this to a singleton entity.
///
/// Ported from: ArchiveMge/Components/AIDebugComponents.cs
/// </summary>
public struct AiDebugConfig
{
    public bool Enabled;

    public bool ShowRoutes;
    public bool ShowBehaviorTargets;
    public bool ShowCoverage;

    /// <summary>
    /// Optional: only show info for this entity id.
    /// </summary>
    public int FocusEntityId;

    public static AiDebugConfig Default => new()
    {
        Enabled = true,
        ShowRoutes = true,
        ShowBehaviorTargets = true,
        ShowCoverage = true,
        FocusEntityId = -1,
    };
}

/// <summary>
/// Per-entity debug data emitted by debug systems for rendering.
///
/// Ported from: ArchiveMge/Components/AIDebugComponents.cs
/// </summary>
public struct AiDebugDrawable
{
    public bool Visible;

    public Vector2? TargetPosition;
    public string? Label;

    // Route preview: origin + next node absolute target.
    public Vector2? RouteOrigin;
    public Vector2? RouteNextTarget;

    // Coverage preview: for man, target entity; for zone, landmark.
    public int ManTargetEntityId;
    public Vector2? ZoneLandmark;
}
