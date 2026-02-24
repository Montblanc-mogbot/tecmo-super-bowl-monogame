namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank27 "misc".
///
/// Keeping this separate from bank26 so we can evolve them independently as we
/// discover actual contents/responsibilities.
/// </summary>
public sealed record MiscBank27Config(
    string Id,
    IReadOnlyDictionary<string, int> IntConstants,
    IReadOnlyDictionary<string, string> StringConstants,
    IReadOnlyDictionary<string, bool> Flags);
