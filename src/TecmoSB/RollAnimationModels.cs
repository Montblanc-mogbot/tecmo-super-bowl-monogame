namespace TecmoSB;

/// <summary>
/// YAML-driven ball roll animation data.
///
/// Based on DOCS/rollanimation.xlsx - animation frames and
/// movement data for the rolling ball.
/// </summary>
public sealed record RollAnimationConfig(
    string Description,
    IReadOnlyList<RomLocation> RomLocations,
    IReadOnlyList<AnimationProperty> AnimationProperties,
    string MonogameNotes,
    IReadOnlyList<string> Notes);

public sealed record RomLocation(
    string Id,
    string Description);

public sealed record AnimationProperty(
    string Property,
    string Description);
