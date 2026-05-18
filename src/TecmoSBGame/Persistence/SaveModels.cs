using System;

namespace TecmoSBGame.Persistence;

public sealed class GameSettings
{
    public int SchemaVersion { get; set; } = 1;
    public float MasterVolume { get; set; } = 1.0f;
    public bool PauseOnFocusLoss { get; set; } = true;
    public string LastSelectedTeam { get; set; } = "BUF";
}

public sealed class SeasonSlotSummary
{
    public int SchemaVersion { get; set; } = 1;
    public string SlotName { get; set; } = "slot-1";
    public int Week { get; set; } = 1;
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SeasonSaveData
{
    public int SchemaVersion { get; set; } = 1;
    public SeasonState Season { get; set; } = new();
}

public sealed class SaveEnvelope<T>
{
    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public T Data { get; set; } = default!;
}
