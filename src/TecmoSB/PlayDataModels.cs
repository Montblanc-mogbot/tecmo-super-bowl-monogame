namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank5/6 offensive/defensive play data.
///
/// Banks 5 and 6 contain the actual play command scripts that define
/// player behavior - routes, blocking assignments, positioning, etc.
/// </summary>
public sealed record PlayDataConfig(
    string Id,
    IReadOnlyList<PlayCommandType> CommandTypes,
    IReadOnlyList<PlayerReactionScript> PlayerReactions,
    IReadOnlyList<PlayCategory> Categories,
    IReadOnlyList<PlayDefinition> Plays,
    PlayDataRomInfo RomInfo,
    IReadOnlyList<string> Notes);

/// <summary>
/// Maps an offensive play number (as referenced by the play list) to per-slot reaction scripts.
/// This is the key bridge between playlist selection and ROM-style player-reaction bytecode.
/// </summary>
public sealed record PlayDefinition(
    int PlayNumber,
    string Description,
    IReadOnlyDictionary<string, string> Offense,
    IReadOnlyDictionary<string, string> Defense);

public sealed record PlayCommandType(
    string Name,
    int Opcode,
    IReadOnlyList<string> Params,
    string Description);

public sealed record PlayerReactionScript(
    string Id,
    string Description,
    string Role,
    IReadOnlyList<PlayCommand> Commands);

public sealed record PlayCommand(
    string Cmd,
    IReadOnlyList<object>? Params,
    string? Target,
    string? Label);

public sealed record PlayCategory(
    string Id,
    string Description,
    IReadOnlyList<int> Reactions);

public sealed record PlayDataRomInfo(
    int BaseAddress,
    int OffenseDataStart,
    int DefenseDataStart,
    int TotalReactions);
