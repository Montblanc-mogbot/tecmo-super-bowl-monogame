namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank21/22 "play commands / on-field logic".
///
/// Think of this as a tiny, data-defined command language ("set velocity",
/// "spawn ball", "whistle", etc.) that gameplay code can interpret.
/// </summary>
public sealed record PlayCommandConfig(
    string Id,
    IReadOnlyDictionary<string, PlayCommandDef> Commands,
    IReadOnlyDictionary<string, PlayCommandProgram> Programs);

public sealed record PlayCommandDef(
    string Id,
    string Kind,
    IReadOnlyDictionary<string, string> Defaults);

public sealed record PlayCommandProgram(
    string Id,
    IReadOnlyList<PlayCommandStep> Steps);

public sealed record PlayCommandStep(
    string Command,
    IReadOnlyDictionary<string, string> Args);
