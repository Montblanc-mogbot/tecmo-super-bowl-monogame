using System;
using System.IO;

namespace TecmoSBGame.Persistence;

public sealed class SavePathResolver
{
    private readonly string _rootDirectory;

    public SavePathResolver(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? ResolveDefaultRoot()
            : Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory => _rootDirectory;
    public string SettingsDirectory => Path.Combine(_rootDirectory, "settings");
    public string SeasonsDirectory => Path.Combine(_rootDirectory, "seasons");
    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public string GetSeasonStatePath(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            throw new ArgumentException("Slot name is required.", nameof(slotName));

        return Path.Combine(SeasonsDirectory, $"{slotName}.season.json");
    }

    public string GetSeasonSlotPath(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            throw new ArgumentException("Slot name is required.", nameof(slotName));

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            slotName = slotName.Replace(invalid, '_');
        }

        return Path.Combine(SeasonsDirectory, $"{slotName}.json");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(SeasonsDirectory);
    }

    private static string ResolveDefaultRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = AppContext.BaseDirectory;

        return Path.Combine(appData, "TecmoSBGame");
    }
}
