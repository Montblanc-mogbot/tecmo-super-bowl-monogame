namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank32 DMC samples and reset vector.
///
/// Bank32 in the original ROM contains DMC (Delta Modulation Channel) audio
/// samples - voice clips and drum sounds used by the NES APU. For MonoGame,
/// these map to audio assets while preserving the playback configuration.
/// </summary>
public sealed record DmcSamplesConfig(
    string Id,
    IReadOnlyList<DmcSample> Samples,
    DmcPlaybackConfig DmcConfig,
    ResetVectorConfig ResetVector,
    AssetMappingConfig AssetMapping);

public sealed record DmcSample(
    string Id,
    string Name,
    int RomOffset,
    int Length,
    string Description,
    string? Note);

public sealed record DmcPlaybackConfig(
    IReadOnlyList<int> PlaybackRates,
    IReadOnlyList<KickPitchVariant> KickPitchVariants);

public sealed record KickPitchVariant(
    int RateIndex,
    string Name);

public sealed record ResetVectorConfig(
    int BaseAddress,
    int PadAddress,
    VectorTable Vectors);

public sealed record VectorTable(
    string Nmi,
    string Reset,
    string Irq);

public sealed record AssetMappingConfig(
    string Format,
    string Directory,
    IReadOnlyList<AssetMapping> Samples);

public sealed record AssetMapping(
    string Id,
    string File);
