namespace TecmoSB;

/// <summary>
/// YAML-driven defensive extra play data.
///
/// Based on DOCS/defextra.txt - describes special defensive
/// formations and player command scripts.
/// </summary>
public sealed record DefExtraConfig(
    IReadOnlyList<DefensiveExtraPlay> DefensiveExtraPlays,
    IReadOnlyList<CommandReference> CommandReference,
    IReadOnlyList<string> Notes);

public sealed record DefensiveExtraPlay(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<DefensivePlayerCommand> Players);

public sealed record DefensivePlayerCommand(
    string Position,
    string Commands);

public sealed record CommandReference(
    string Name,
    string Description,
    string? Param);
