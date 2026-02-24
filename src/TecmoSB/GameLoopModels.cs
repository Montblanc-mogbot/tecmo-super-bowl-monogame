namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for the classic bank17/18 "main game loop".
///
/// Goal: model the high-level state machine as data (YAML) + a small runner,
/// rather than re-creating NES-era RAM/ROM coupling.
/// </summary>
public sealed record GameLoopConfig(
    string Id,
    string InitialState,
    IReadOnlyDictionary<string, GameLoopState> States);

public sealed record GameLoopState(
    string Id,
    string Kind,
    string? Next,
    IReadOnlyDictionary<string, string> OnEvent);

public sealed record GameLoopSnapshot(
    string ConfigId,
    string CurrentStateId,
    int TickCount);
