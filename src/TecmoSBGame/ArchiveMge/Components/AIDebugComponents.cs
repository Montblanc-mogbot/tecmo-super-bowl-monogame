using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Global toggle + options for AI debug visualization.
/// Attach this to a singleton entity.
/// </summary>
public sealed class AIDebugConfigComponent
{
    public bool Enabled { get; set; } = true;

    public bool ShowRoutes { get; set; } = true;
    public bool ShowBehaviorTargets { get; set; } = true;
    public bool ShowCoverage { get; set; } = true;

    /// <summary>
    /// Optional: only show info for this entity id.
    /// </summary>
    public int FocusEntityId { get; set; } = -1;
}

/// <summary>
/// Per-entity debug data emitted by AIDebugSystem for rendering.
/// </summary>
public sealed class AIDebugDrawableComponent
{
    public bool Visible { get; set; }

    public Vector2? TargetPosition { get; set; }
    public string? Label { get; set; }

    // Route preview: origin + next node absolute target.
    public Vector2? RouteOrigin { get; set; }
    public Vector2? RouteNextTarget { get; set; }

    // Coverage preview: for man, target entity; for zone, landmark.
    public int ManTargetEntityId { get; set; } = -1;
    public Vector2? ZoneLandmark { get; set; }
}
