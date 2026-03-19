namespace TecmoSB;

/// <summary>
/// YAML-driven defensive formation placement data.
///
/// Clean-room model: positions only. Behavior is handled by play scripts + defense execution systems.
/// </summary>
public sealed record DefensiveFormationDataConfig(
    IReadOnlyList<DefensiveFormation> DefensiveFormations,
    IReadOnlyList<string> Notes);

public sealed record DefensiveFormation(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<DefensiveFormationPlayer> Players);

public sealed record DefensiveFormationPlayer(
    string Slot,
    string Offset);
