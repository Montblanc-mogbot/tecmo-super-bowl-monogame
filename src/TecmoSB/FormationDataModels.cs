namespace TecmoSB;

/// <summary>
/// YAML-driven offensive formation data.
///
/// Based on DOCS/formation.txt - detailed offensive formation
/// definitions with player commands.
/// </summary>
public sealed record FormationDataConfig(
    IReadOnlyList<OffensiveFormation> OffensiveFormations,
    IReadOnlyList<CommandReference> CommandReference,
    IReadOnlyList<FormationType> FormationTypes,
    IReadOnlyList<string> Notes);

public sealed record OffensiveFormation(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<FormationPlayer> Players);

public sealed record FormationPlayer(
    string Position,
    string Address,
    string Commands);

public sealed record FormationType(
    string Id,
    IReadOnlyList<string> FormationIds);
