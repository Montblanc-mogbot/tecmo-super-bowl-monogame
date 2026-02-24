namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank3 formation and metatile data.
///
/// Bank3 in the original ROM contains offensive formation pointers and
/// metatile tile data. Formations define player positions and initial
/// reactions for various offensive sets (Pro-T, Shotgun, Kickoff, etc.).
/// </summary>
public sealed record FormationConfig(
    string Id,
    IReadOnlyList<string> FormationTypes,
    IReadOnlyList<FormationPointerSet> FormationPointers,
    IReadOnlyList<PlayerReaction> PlayerReactions,
    MetatileConfig MetatileConfig,
    RomInfo RomInfo,
    IReadOnlyList<string> Notes);

public sealed record FormationPointerSet(
    string Formation,
    int TypeId,
    IReadOnlyList<PlayerReactionRef> PlayerReactions);

public sealed record PlayerReactionRef(
    int Index,
    string ReactionId);

public sealed record PlayerReaction(
    string Id,
    string Description);

public sealed record MetatileConfig(
    int TileSize,
    int MetatileSize,
    IReadOnlyList<string> Categories);

public sealed record RomInfo(
    int BaseAddress,
    int FormationPointersStart,
    int FormationDataStart);
