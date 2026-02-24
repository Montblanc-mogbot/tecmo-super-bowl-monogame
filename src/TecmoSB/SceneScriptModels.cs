namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank7 scene scripts.
///
/// Bank7 contains scene script bytecode that controls visual presentation
/// including PPU operations, scrolling, palette fades, and background drawing.
/// </summary>
public sealed record SceneScriptConfig(
    string Id,
    IReadOnlyList<SceneOpcode> Opcodes,
    IReadOnlyList<string> SceneTypes,
    IReadOnlyList<SceneScript> SceneScripts,
    SceneScriptRomInfo RomInfo,
    IReadOnlyList<string> Notes);

public sealed record SceneOpcode(
    int Code,
    string Name,
    IReadOnlyList<string> Params,
    string Description);

public sealed record SceneScript(
    string Id,
    string Description,
    IReadOnlyList<SceneCommand> Commands);

public sealed record SceneCommand(
    string Opcode,
    IReadOnlyList<int>? Params);

public sealed record SceneScriptRomInfo(
    int BaseAddress,
    int MacroSection,
    int ScriptDataStart);
