using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TecmoSBGame.SimArch.Headless;

/// <summary>
/// NES trace JSON models.
///
/// Ported from: ArchiveMge/Headless/NesTraceModels.cs
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
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class NesTraceFrame
{
    [JsonPropertyName("tick")]
    public int Tick { get; set; }

    [JsonPropertyName("pos")]
    public Dictionary<string, NesTracePos> Positions { get; set; } = new();
}

public readonly record struct NesTracePos(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y);
