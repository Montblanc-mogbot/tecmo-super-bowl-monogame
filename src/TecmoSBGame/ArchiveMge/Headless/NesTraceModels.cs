using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TecmoSBGame.Headless;

/// <summary>
/// Minimal JSON schema for a recorded NES trace.
///
/// This is a scaffold to support assembly-vs-sim comparisons.
/// Expected: one frame per 60Hz tick.
/// </summary>
public sealed class NesTrace
{
    [JsonPropertyName("meta")]
    public NesTraceMeta Meta { get; set; } = new();

    [JsonPropertyName("frames")]
    public List<NesTraceFrame> Frames { get; set; } = new();
}

public sealed class NesTraceMeta
{
    [JsonPropertyName("game")]
    public string Game { get; set; } = "TecmoSuperBowl";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("playId")]
    public int PlayId { get; set; }

    [JsonPropertyName("startAbsoluteYard")]
    public int StartAbsoluteYard { get; set; }
}

public sealed class NesTraceFrame
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

    // Optional: ball / qb / receiver positions keyed by role or entity label.
    [JsonPropertyName("positions")]
    public Dictionary<string, NesTracePos> Positions { get; set; } = new();
}

public readonly record struct NesTracePos(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y);
