using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TecmoSBGame.Replay;

/// <summary>
/// Minimal replay capture format.
///
/// Goal: produce a deterministic per-tick snapshot stream that can be diffed against
/// an emulator trace or another sim run.
///
/// This is intentionally small and JSON-friendly.
/// </summary>
public sealed class ReplayCapture
{
    [JsonPropertyName("meta")]
    public ReplayMeta Meta { get; set; } = new();

    [JsonPropertyName("frames")]
    public List<ReplayFrame> Frames { get; set; } = new();
}

public sealed class ReplayMeta
{
    [JsonPropertyName("playId")]
    public int PlayId { get; set; }

    [JsonPropertyName("startAbsoluteYard")]
    public int StartAbsoluteYard { get; set; }

    [JsonPropertyName("seed")]
    public uint DeterministicSeed { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class ReplayFrame
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

    /// <summary>
    /// Positions keyed by entity id string.
    /// </summary>
    [JsonPropertyName("pos")]
    public Dictionary<string, ReplayPos> Positions { get; set; } = new();

    [JsonPropertyName("ball")]
    public ReplayBallState Ball { get; set; } = new();
}

public readonly record struct ReplayPos(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y);

public sealed class ReplayBallState
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("owner")]
    public int? OwnerEntityId { get; set; }
}
