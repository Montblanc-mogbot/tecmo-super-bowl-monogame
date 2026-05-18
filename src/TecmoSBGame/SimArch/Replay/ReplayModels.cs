using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TecmoSBGame.SimArch.Replay;

/// <summary>
/// Minimal replay capture format.
///
/// Ported from: ArchiveMge/Replay/ReplayModels.cs
/// </summary>
public sealed class ReplayCapture
{
    [JsonPropertyName("meta")]
    public ReplayMeta Meta { get; set; } = new();

    [JsonPropertyName("frames")]
    public List<ReplayFrame> Frames { get; set; } = new();

    [JsonPropertyName("events")]
    public List<ReplayEvent> Events { get; set; } = new();
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

    [JsonPropertyName("finalAbsoluteYard")]
    public int FinalAbsoluteYard { get; set; }
}

public sealed class ReplayFrame
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

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

public sealed class ReplayEvent
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public int TeamIndex { get; set; }

    [JsonPropertyName("player")]
    public int? PlayerId { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("turnover")]
    public bool Turnover { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}
