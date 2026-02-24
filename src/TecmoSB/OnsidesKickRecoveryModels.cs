namespace TecmoSB;

/// <summary>
/// YAML-driven onside kick recovery rate data.
///
/// Based on DOCS/onsides_kick_recovery_rates.xlsx - documents
/// the P1 advantage bug in onside kick recovery.
/// </summary>
public sealed record OnsidesKickRecoveryConfig(
    string Description,
    string BugExplanation,
    IReadOnlyList<TeamRecoveryData> TeamRecoveryData,
    IReadOnlyList<PowerBarLevel> PowerBarLevels,
    string RecoveryMechanics,
    string FixRecommendation,
    string DataFormat,
    IReadOnlyList<string> Notes);

public sealed record TeamRecoveryData(
    string Team,
    string P1RecoveryHex,
    string P2RecoveryHex);

public sealed record PowerBarLevel(
    string Level,
    string Description,
    int? P1Distance,
    int? P2Distance,
    bool? BugAffected);
