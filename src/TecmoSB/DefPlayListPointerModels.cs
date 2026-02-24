namespace TecmoSB;

/// <summary>
/// YAML-driven defensive play list pointer codes.
///
/// Based on DOCS/defplaylistpointercode.xlsx - maps defensive
/// formations/play IDs to player reaction assignments.
/// </summary>
public sealed record DefPlayListPointerConfig(
    IReadOnlyList<DefensivePlayPointer> DefensivePlayPointers,
    IReadOnlyDictionary<string, string> ReferenceMappings,
    PlayerPositionMapping PlayerPositions,
    IReadOnlyList<string> Notes);

public sealed record DefensivePlayPointer(
    int PlayId,
    string Description,
    IReadOnlyList<int> PlayerReactions,
    IReadOnlyList<int>? ReferenceIds);

public sealed record PlayerPositionMapping(
    string Index0,
    string Index1,
    string Index2,
    string Index3,
    string Index4,
    string Index5,
    string Index6,
    string Index7,
    string Index8,
    string Index9,
    string Index10);
