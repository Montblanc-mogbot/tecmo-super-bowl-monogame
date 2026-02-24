namespace TecmoSB;

public sealed record SimConfig(
    int MaxScoreLimit,
    int YardsForFirstDown,
    int MinutesPerQuarter,
    int LengthOfFieldYards,
    int XpKickDistanceYards
);
