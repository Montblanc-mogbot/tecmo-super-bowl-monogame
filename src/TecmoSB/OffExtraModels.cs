namespace TecmoSB;

/// <summary>
/// YAML-driven offensive extra play data.
///
/// Based on DOCS/offextra.txt - special offensive formations
/// and player command scripts.
/// </summary>
public sealed record OffExtraConfig(
    IReadOnlyList<OffensiveExtraPlay> OffensiveExtraPlays,
    IReadOnlyList<CommandReference> CommandReference,
    IReadOnlyList<string> Notes);

public sealed record OffensiveExtraPlay(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<OffensivePlayerCommand> Players);

public sealed record OffensivePlayerCommand(
    string Position,
    string Commands);
