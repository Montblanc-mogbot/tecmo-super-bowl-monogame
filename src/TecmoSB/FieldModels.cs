namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank23 drawing field, ball animation, and collision checks.
///
/// This is not a renderer/physics engine yet; it's configuration describing:
/// - Field layout identity (what background/metatile set to use)
/// - Ball animation presets
/// - Simple collision volumes / boundaries (for later integration)
/// </summary>
public sealed record FieldConfig(
    string Id,
    string FieldLayoutId,
    IReadOnlyDictionary<string, BallAnimationDef> BallAnimations,
    IReadOnlyList<FieldBoundary> Boundaries);

public sealed record BallAnimationDef(
    string Id,
    IReadOnlyList<BallAnimationFrame> Frames,
    bool Loop);

public sealed record BallAnimationFrame(
    string SpriteId,
    int Duration);

public sealed record FieldBoundary(
    string Id,
    string Kind,
    float X,
    float Y,
    float Width,
    float Height);
