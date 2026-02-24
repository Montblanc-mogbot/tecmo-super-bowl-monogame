namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank25 (leaders screens, player data display, pro-bowl abbreviations).
///
/// We treat this as presentation-oriented data: leaderboard categories and how to
/// label/sort them.
/// </summary>
public sealed record LeadersConfig(
    string Id,
    IReadOnlyList<LeaderCategory> Categories,
    IReadOnlyDictionary<string, string> ProBowlAbbrevs);

public sealed record LeaderCategory(
    string Id,
    string Title,
    string StatKey,
    int TopN);
