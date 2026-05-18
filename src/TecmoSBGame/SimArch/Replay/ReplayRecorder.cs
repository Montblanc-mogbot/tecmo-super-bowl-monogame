using System;
using System.IO;
using System.Text.Json;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Replay;

/// <summary>
/// Owns a replay capture and can flush it to disk.
///
/// Ported from: ArchiveMge/Replay/ReplayRecorder.cs
/// </summary>
public sealed class ReplayRecorder
{
    public ReplayCapture Capture { get; } = new();

    public bool Enabled { get; set; }

    public void ResetForPlay(SimArch.State.PlayState play)
    {
        Capture.Meta.PlayId = play.PlayId;
        Capture.Meta.StartAbsoluteYard = play.StartAbsoluteYard;
        Capture.Meta.FinalAbsoluteYard = play.StartAbsoluteYard;
        Capture.Meta.DeterministicSeed = play.DeterministicSeed;
        Capture.Frames.Clear();
        Capture.Events.Clear();
    }

    public void RecordEvent(int tick, StatEventRecord record)
    {
        Capture.Events.Add(new ReplayEvent
        {
            Tick = tick,
            Type = record.EventType,
            TeamIndex = record.TeamIndex,
            PlayerId = record.PlayerId,
            Yards = record.Yards,
            Turnover = record.Turnover,
            Detail = record.Detail,
        });
    }

    public string SaveJson(string? dir = null)
    {
        dir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TecmoSBGame",
            "Replays");

        Directory.CreateDirectory(dir);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(dir, $"replay_play{Capture.Meta.PlayId}_{stamp}.json");

        var json = JsonSerializer.Serialize(Capture, new JsonSerializerOptions { WriteIndented = false });

        File.WriteAllText(path, json);
        return path;
    }
}
