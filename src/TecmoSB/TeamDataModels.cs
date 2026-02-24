namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank1/2 "team data".
///
/// This is a modernized representation (teams + basic metadata). As we port more
/// systems, we can extend this with playbooks, rosters, uniforms, etc.
/// </summary>
public sealed record TeamDataConfig(
    string Id,
    IReadOnlyList<TeamDefinition> Teams);

public sealed record TeamDefinition(
    string Id,
    string City,
    string Name,
    string Abbrev,
    TeamColors Colors);

public sealed record TeamColors(
    string Primary,
    string Secondary,
    string? Accent);
