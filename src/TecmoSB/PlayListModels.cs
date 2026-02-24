namespace TecmoSB;

/// <summary>
/// YAML-driven play list data.
///
/// Based on DOCS/playlist.txt - complete list of offensive plays
/// with formations and defensive matchups.
/// </summary>
public sealed record PlayListConfig(
    IReadOnlyList<PlayEntry> PlayList,
    IReadOnlyList<SlotDefinition> Slots,
    IReadOnlyList<string> Notes);

public sealed record PlayEntry(
    string Name,
    string Slot,
    string Formation,
    IReadOnlyList<int> PlayNumbers,
    IReadOnlyList<string> Defense);

public sealed record SlotDefinition(
    string Id,
    string Name,
    string Description);
