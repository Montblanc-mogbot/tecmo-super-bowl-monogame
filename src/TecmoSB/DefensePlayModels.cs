namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank4 defense play and special teams pointers.
///
/// Bank4 in the original ROM contains defensive execution play pointers and
/// special teams data. Each defensive execution defines 11 player reactions.
/// </summary>
public sealed record DefensePlayConfig(
    string Id,
    IReadOnlyList<DefensiveExecution> DefensiveExecutions,
    IReadOnlyList<DefensePlayerReaction> DefensePlayerReactions,
    IReadOnlyList<SpecialTeamsExecution> SpecialTeamsExecutions,
    DefenseRomInfo RomInfo,
    IReadOnlyList<string> Notes);

public sealed record DefensiveExecution(
    string Id,
    string Description,
    IReadOnlyList<PlayerReactionRef> PlayerReactions);

public sealed record DefensePlayerReaction(
    string Id,
    string Description,
    string Role);

public sealed record SpecialTeamsExecution(
    string Id,
    string Description);

public sealed record DefenseRomInfo(
    int BaseAddress,
    int DefensePointersStart,
    int NumDefensiveExecutions);
