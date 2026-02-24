namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank29 sound data.
///
/// Bank29/30 in the original ROM tend to be data-heavy (instrument tables, note
/// sequences, etc.). We model it as song + sfx "patterns" that can later be
/// compiled into a runtime playback format.
/// </summary>
public sealed record SoundDataConfig(
    string Id,
    IReadOnlyList<SongDef> Songs,
    IReadOnlyList<SfxDef> Sfx);

public sealed record SongDef(
    string Id,
    int Tempo,
    IReadOnlyList<SoundPattern> Patterns);

public sealed record SfxDef(
    string Id,
    IReadOnlyList<SoundPattern> Patterns);

public sealed record SoundPattern(
    string Channel,
    IReadOnlyList<SoundNote> Notes);

public sealed record SoundNote(
    string Pitch,
    int Duration);
