namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank9 sprite scripts.
///
/// Bank9 contains sprite script bytecode for animating sprites
/// - player animations, ball physics, effects, team logos, etc.
/// </summary>
public sealed record Bank9SpriteScriptConfig(
    string Id,
    IReadOnlyList<SpriteOpcode> Opcodes,
    IReadOnlyList<Bank9SpriteScriptDef> SpriteScripts,
    IReadOnlyList<TeamLogoSprite> TeamLogos,
    IReadOnlyList<string> Categories,
    SpriteScriptRomInfo RomInfo,
    IReadOnlyList<string> Notes);

public sealed record SpriteOpcode(
    int Code,
    string Name,
    IReadOnlyList<string> Params,
    string Description);

public sealed record Bank9SpriteScriptDef(
    int Id,
    string Name,
    string Description,
    string Category);

public sealed record TeamLogoSprite(
    int Id,
    string Team);

public sealed record SpriteScriptRomInfo(
    int BaseAddress,
    int PointerTableStart,
    int ScriptDataStart,
    int NumScripts);
