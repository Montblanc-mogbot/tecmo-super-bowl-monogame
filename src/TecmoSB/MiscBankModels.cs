namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank26 "misc".
///
/// The original bank mixes small utilities, tables, and one-off rules.
/// Here we provide a grab-bag config container we can split later as the real
/// responsibilities become clear.
/// </summary>
public sealed record MiscBankConfig(
    string Id,
    IReadOnlyDictionary<string, int> IntConstants,
    IReadOnlyDictionary<string, string> StringConstants,
    IReadOnlyDictionary<string, bool> Flags);
