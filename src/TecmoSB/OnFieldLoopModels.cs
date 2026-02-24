namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank19/20 "on-field gameplay loop".
///
/// This captures a high-level phase/state machine (pre-snap → snap → live play → whistle/resolve)
/// without binding to NES-era RAM addresses.
/// </summary>
public sealed record OnFieldLoopConfig(
    string Id,
    string InitialState,
    IReadOnlyDictionary<string, OnFieldState> States);

public sealed record OnFieldState(
    string Id,
    string Kind,
    string? Next,
    IReadOnlyDictionary<string, string> OnEvent);

public sealed record OnFieldLoopSnapshot(
    string ConfigId,
    string CurrentStateId,
    int TickCount);
