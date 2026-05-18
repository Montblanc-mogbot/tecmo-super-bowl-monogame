using System;
using System.IO;
using System.Text.Json;

namespace TecmoSBGame.Persistence;

public sealed class SaveManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SavePathResolver _paths;

    public SaveManager(SavePathResolver? paths = null)
    {
        _paths = paths ?? new SavePathResolver();
        _paths.EnsureDirectories();
    }

    public SavePathResolver Paths => _paths;

    public void SaveSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SaveEnvelope(_paths.SettingsFilePath, "settings", settings.SchemaVersion, settings);
    }

    public GameSettings LoadSettingsOrDefault()
    {
        return LoadEnvelope<GameSettings>(_paths.SettingsFilePath, "settings")?.Data ?? new GameSettings();
    }

    public void SaveSeasonSlot(SeasonSlotSummary slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        slot.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveEnvelope(_paths.GetSeasonSlotPath(slot.SlotName), "season-slot", slot.SchemaVersion, slot);
    }

    public SeasonSlotSummary? LoadSeasonSlot(string slotName)
    {
        return LoadEnvelope<SeasonSlotSummary>(_paths.GetSeasonSlotPath(slotName), "season-slot")?.Data;
    }

    public void SaveSeasonState(SeasonState season)
    {
        ArgumentNullException.ThrowIfNull(season);
        season.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SaveEnvelope(_paths.GetSeasonStatePath(season.SlotName), "season-state", season.SchemaVersion, new SeasonSaveData { SchemaVersion = season.SchemaVersion, Season = season });
    }

    public SeasonState? LoadSeasonState(string slotName)
    {
        return LoadEnvelope<SeasonSaveData>(_paths.GetSeasonStatePath(slotName), "season-state")?.Data?.Season;
    }

    private void SaveEnvelope<T>(string path, string kind, int version, T data)
    {
        var envelope = new SaveEnvelope<T>
        {
            Kind = kind,
            Version = version,
            SavedAtUtc = DateTimeOffset.UtcNow,
            Data = data,
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static SaveEnvelope<T>? LoadEnvelope<T>(string path, string expectedKind)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        var envelope = JsonSerializer.Deserialize<SaveEnvelope<T>>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize save envelope: {path}");

        if (!string.Equals(envelope.Kind, expectedKind, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unexpected save kind '{envelope.Kind}' in {path}; expected '{expectedKind}'.");

        return envelope;
    }
}
