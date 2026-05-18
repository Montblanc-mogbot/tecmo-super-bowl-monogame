using System;

namespace TecmoSBGame.RuntimeCapture;

public sealed class CaptureManifest
{
    public string TimestampUtc { get; set; } = string.Empty;
    public int CapturedTick { get; set; }
    public int RequestedTicks { get; set; }
    public int Quarter { get; set; }
    public int GameClockSeconds { get; set; }
    public int PossessionTeam { get; set; }
    public int PlayNumber { get; set; }
    public string PlayPhase { get; set; } = string.Empty;
    public string StatusLine { get; set; } = string.Empty;
    public string SituationLabel { get; set; } = string.Empty;
    public string LastPlaySummary { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
}
