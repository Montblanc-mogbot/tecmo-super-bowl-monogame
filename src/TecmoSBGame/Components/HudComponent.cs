namespace TecmoSBGame.Components;

/// <summary>
/// Tags an entity as a HUD element and stores the latest bound display values.
///
/// Notes:
/// - Rendering is handled by dedicated renderer classes (not ECS systems).
/// - This component is intentionally lightweight and UI-focused.
/// </summary>
public sealed class HudComponent
{
    public HudElementType ElementType { get; set; }

    /// <summary>
    /// Whether the HUD element should be considered visible for the current <see cref="TecmoSBGame.Flow.GameFlowState"/>.
    /// Renderers may ignore this and instead check flow state directly.
    /// </summary>
    public bool Visible { get; set; } = true;

    // Generic, bindable fields. Different HUD element types can interpret these as needed.
    public int Int0 { get; set; }
    public int Int1 { get; set; }
    public int Int2 { get; set; }

    public string Text0 { get; set; } = string.Empty;
    public string Text1 { get; set; } = string.Empty;
}

public enum HudElementType
{
    Scoreboard = 0,
    DownDistance = 1,
    PlayClock = 2,
    PossessionIndicator = 3,
}
