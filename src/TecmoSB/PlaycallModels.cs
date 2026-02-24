namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank20 "playcall".
///
/// This defines a set of playcall screens/contexts and the menu options available.
/// It is intentionally abstract (no NES PPU coordinates). UI can render these as
/// modern menus.
/// </summary>
public sealed record PlaycallConfig(
    string Id,
    IReadOnlyDictionary<string, PlaycallScreen> Screens);

public sealed record PlaycallScreen(
    string Id,
    string Title,
    IReadOnlyList<PlaycallOption> Options);

public sealed record PlaycallOption(
    string Id,
    string Label,
    string? NextScreen,
    string? EmitEvent);
