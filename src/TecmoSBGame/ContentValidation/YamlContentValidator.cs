using System;
using System.Collections.Generic;
using System.Linq;
using TecmoSB;

namespace TecmoSBGame.ContentValidation;

public static class YamlContentValidator
{
    public sealed record ValidationIssue(string ContentPath, string Message)
    {
        public override string ToString() => $"{ContentPath}: {Message}";
    }

    public static IReadOnlyList<ValidationIssue> Validate(
        FormationDataConfig formationData,
        PlayListConfig playList,
        PlayDataConfig playData)
    {
        var issues = new List<ValidationIssue>();

        ValidateFormationData(formationData, issues);
        ValidatePlayData(playData, issues);
        ValidatePlayList(playList, formationData, playData, issues);

        return issues;
    }

    private static void ValidateFormationData(FormationDataConfig formationData, List<ValidationIssue> issues)
    {
        var formationIds = new HashSet<string>(
            formationData.OffensiveFormations.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var t in formationData.FormationTypes)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                issues.Add(new ValidationIssue("formations/formation_data.yaml", "FormationType.id is required"));
                continue;
            }

            foreach (var fid in t.FormationIds)
            {
                if (string.IsNullOrWhiteSpace(fid))
                {
                    issues.Add(new ValidationIssue(
                        "formations/formation_data.yaml",
                        $"FormationType '{t.Id}' contains an empty formation_id"));
                    continue;
                }

                if (!formationIds.Contains(fid))
                {
                    issues.Add(new ValidationIssue(
                        "formations/formation_data.yaml",
                        $"FormationType '{t.Id}' references unknown formation_id '{fid}'"));
                }
            }
        }

        var dupes = formationData.OffensiveFormations
            .GroupBy(f => f.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        foreach (var d in dupes)
            issues.Add(new ValidationIssue("formations/formation_data.yaml", $"Duplicate OffensiveFormation.id '{d}'"));
    }

    private static void ValidatePlayData(PlayDataConfig playData, List<ValidationIssue> issues)
    {
        var playNumbers = new HashSet<int>();
        foreach (var p in playData.Plays)
        {
            if (p.PlayNumber <= 0)
            {
                issues.Add(new ValidationIssue("playdata/bank5_6_play_data.yaml", "PlayDefinition.play_number must be > 0"));
                continue;
            }

            // Tecmo play numbers are effectively bytes in many places; keep this constraint explicit.
            if (p.PlayNumber > 0xFF)
            {
                issues.Add(new ValidationIssue(
                    "playdata/bank5_6_play_data.yaml",
                    $"PlayDefinition.play_number {p.PlayNumber} is out of range (expected 1..255)"));
            }

            if (!playNumbers.Add(p.PlayNumber))
            {
                issues.Add(new ValidationIssue(
                    "playdata/bank5_6_play_data.yaml",
                    $"Duplicate PlayDefinition.play_number {p.PlayNumber}"));
            }
        }
    }

    private static void ValidatePlayList(
        PlayListConfig playList,
        FormationDataConfig formationData,
        PlayDataConfig playData,
        List<ValidationIssue> issues)
    {
        var slotIds = new HashSet<string>(
            playList.Slots.Select(s => s.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        var formationIds = new HashSet<string>(
            formationData.OffensiveFormations.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        var validPlayNumbers = new HashSet<int>(playData.Plays.Select(p => p.PlayNumber));

        for (var i = 0; i < playList.PlayList.Count; i++)
        {
            var entry = playList.PlayList[i];
            var entryName = !string.IsNullOrWhiteSpace(entry.Name) ? entry.Name : $"(index {i})";

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                issues.Add(new ValidationIssue(
                    "playcall/playlist.yaml",
                    $"PlayEntry.name is required (entry index {i})"));
            }

            if (!string.IsNullOrWhiteSpace(entry.Slot) && !slotIds.Contains(entry.Slot))
            {
                issues.Add(new ValidationIssue(
                    "playcall/playlist.yaml",
                    $"PlayEntry '{entryName}' references unknown slot '{entry.Slot}'"));
            }

            if (!string.IsNullOrWhiteSpace(entry.Formation) && !formationIds.Contains(entry.Formation))
            {
                issues.Add(new ValidationIssue(
                    "playcall/playlist.yaml",
                    $"PlayEntry '{entryName}' references unknown formation '{entry.Formation}'"));
            }

            foreach (var playNo in entry.PlayNumbers ?? Array.Empty<int>())
            {
                if (playNo <= 0)
                {
                    issues.Add(new ValidationIssue(
                        "playcall/playlist.yaml",
                        $"PlayEntry '{entryName}' contains out-of-range play_number {playNo} (expected > 0)"));
                    continue;
                }

                if (playNo > 0xFF)
                {
                    issues.Add(new ValidationIssue(
                        "playcall/playlist.yaml",
                        $"PlayEntry '{entryName}' contains out-of-range play_number {playNo} (expected 1..255)"));
                }

                if (!validPlayNumbers.Contains(playNo))
                {
                    issues.Add(new ValidationIssue(
                        "playcall/playlist.yaml",
                        $"PlayEntry '{entryName}' references missing play_number {playNo} (not present in playdata)"));
                }
            }
        }

        // Slots: id required + unique.
        var slotDupeIds = playList.Slots
            .GroupBy(s => s.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();
        foreach (var d in slotDupeIds)
            issues.Add(new ValidationIssue("playcall/playlist.yaml", $"Duplicate SlotDefinition.id '{d}'"));

        foreach (var s in playList.Slots)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                issues.Add(new ValidationIssue("playcall/playlist.yaml", "SlotDefinition.id is required"));
        }
    }
}
