namespace TecmoSB;

/// <summary>
/// YAML-driven yardline reference data.
///
/// Based on DOCS/tecmo_yardline_reference.txt - yardline
/// positions and their internal game values.
/// </summary>
public sealed record YardlineReferenceConfig(
    string Description,
    IReadOnlyList<YardlineEntry> Yardlines,
    IReadOnlyList<string> Notes);

public sealed record YardlineEntry(
    int Yard,
    int Hb,
    int Lb,
    string TwoByte);
