namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank24 "draw script engine".
///
/// The NES version is a compact bytecode. Here we model it as a list of typed
/// ops that a renderer/UI layer can interpret.
/// </summary>
public sealed record DrawScript(
    string Id,
    IReadOnlyList<DrawOp> Ops);

public sealed record DrawOp(
    string Kind,
    IReadOnlyDictionary<string, string> Args);
