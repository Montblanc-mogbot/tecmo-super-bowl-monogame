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
    PlayDataRomInfo RomInfo,
    IReadOnlyList<string> Notes);

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
