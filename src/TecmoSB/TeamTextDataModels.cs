namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank16 team text data.
///
/// Bank16 contains team abbreviations, city names, mascots,
/// and various UI text strings (downs, divisions, conferences, etc.)
/// </summary>
public sealed record TeamTextDataConfig(
    string Id,
    IReadOnlyList<TeamTextEntry> Teams,
    IReadOnlyList<ConferenceText> Conferences,
    IReadOnlyList<DivisionText> Divisions,
    IReadOnlyList<DownName> DownNames,
    IReadOnlyList<ControlType> ControlTypes,
    OffenseDefenseLabels OffenseDefenseLabels,
    TeamTextRomInfo RomInfo,
    IReadOnlyList<string> Notes);

public sealed record TeamTextEntry(
    string Id,
    string Abbreviation,
    string City,
    string Mascot,
    string Conference,
    string Division);

public sealed record ConferenceText(
    string Id,
    string Abbreviation,
    string FullName);

public sealed record DivisionText(
    string Id,
    string Name);

public sealed record DownName(
    int Number,
    string Name);

public sealed record ControlType(
    string Id,
    string Short,
    string Description);

public sealed record OffenseDefenseLabels(
    string Offense,
    string Defense);

public sealed record TeamTextRomInfo(
    int BaseAddress,
    int DataStart,
    int AbbreviationPointers,
    int CityPointers,
    int MascotPointers,
    int DownTextPointers,
    int ControlTypePointers);
