namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank28 sound engine.
///
/// This is a declarative sound catalog + simple event-to-sound mapping.
/// Actual audio playback implementation (MonoGame SoundEffect/Song, mixing,
/// priority/ducking, etc.) can be added later.
/// </summary>
public sealed record SoundEngineConfig(
    string Id,
    IReadOnlyDictionary<string, SoundDef> Sounds,
    IReadOnlyDictionary<string, string> EventMap);

public sealed record SoundDef(
    string Id,
    string Kind,
    string Asset,
    float Volume,
    bool Loop);
