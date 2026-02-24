namespace TecmoSB;

/// <summary>
/// YAML-driven field goal mechanics data.
///
/// Based on DOCS/FG_worksheet.xlsx - defines how field goal success
/// is calculated including distance multipliers, lookup tables, and
/// notch thresholds for the power bar mechanic.
/// </summary>
public sealed record FgWorksheetConfig(
    string Id,
    IReadOnlyList<LosToFgDistance> DistanceMultipliers,
    RandomModifierConfig RandomModifier,
    MaxArrowRangeConfig MaxArrowRange,
    SuccessLookupTable SuccessLookupTable,
    NotchThresholdsConfig NotchThresholds,
    AutoTapConfig AutoTap,
    IReadOnlyList<string> Notes);

public sealed record LosToFgDistance(
    int Los,
    int FgDistance,
    double Multiplier);

public sealed record RandomModifierConfig(
    IReadOnlyList<int> Values,
    double ProbabilityEach,
    bool Special50KaKicker);

public sealed record MaxArrowRangeConfig(
    string Formula);

public sealed record SuccessLookupTable(
    IReadOnlyList<SuccessLookupEntry> Entries);

public sealed record SuccessLookupEntry(
    int Index,
    int Los,
    int FgDistance,
    string Multiplier,
    string ResultGood,
    string ResultMiss,
    int MaxNotchesPerfect,
    int MaxNotchesGood,
    int MaxNotchesTotal);

public sealed record NotchThresholdsConfig(
    string PerfectCenter,
    string PerDistanceIncrease);

public sealed record AutoTapConfig(
    bool Enabled,
    int LosThreshold,
    string Description);
