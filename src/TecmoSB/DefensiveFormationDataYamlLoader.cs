using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DefensiveFormationDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private sealed class DefensiveFormationDataConfigDto
    {
        public List<DefensiveFormationDto> DefensiveFormations { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public DefensiveFormationDataConfig ToModel()
        {
            var forms = new List<DefensiveFormation>(DefensiveFormations.Count);
            foreach (var f in DefensiveFormations)
                forms.Add(f.ToModel());

            return new DefensiveFormationDataConfig(forms, Notes);
        }
    }

    private sealed class DefensiveFormationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DefensiveFormationPlayerDto> Players { get; set; } = new();

        public DefensiveFormation ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DefensiveFormation.id is required");

            var ps = new List<DefensiveFormationPlayer>(Players.Count);
            foreach (var p in Players)
                ps.Add(p.ToModel());

            return new DefensiveFormation(Id, Name, Description, ps);
        }
    }

    private sealed class DefensiveFormationPlayerDto
    {
        public string Slot { get; set; } = string.Empty;
        public string Offset { get; set; } = string.Empty;

        public DefensiveFormationPlayer ToModel()
        {
            if (string.IsNullOrWhiteSpace(Slot))
                throw new InvalidDataException("DefensiveFormationPlayer.slot is required");
            if (string.IsNullOrWhiteSpace(Offset))
                throw new InvalidDataException("DefensiveFormationPlayer.offset is required");

            return new DefensiveFormationPlayer(Slot, Offset);
        }
    }

    public static DefensiveFormationDataConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Defensive formation data not found: {path}");

        var yaml = File.ReadAllText(path);

        var dto = Deserializer.Deserialize<DefensiveFormationDataConfigDto>(yaml);
        return dto.ToModel();
    }
}
