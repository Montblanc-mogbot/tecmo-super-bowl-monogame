using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class TeamTextDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static TeamTextDataConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<TeamTextDataConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class TeamTextDataConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<TeamTextEntryYamlDto> Teams { get; set; } = new();
        public List<ConferenceTextYamlDto> Conferences { get; set; } = new();
        public List<DivisionTextYamlDto> Divisions { get; set; } = new();
        public List<DownNameYamlDto> DownNames { get; set; } = new();
        public List<ControlTypeYamlDto> ControlTypes { get; set; } = new();
        public OffenseDefenseLabelsYamlDto OffenseDefenseLabels { get; set; } = new();
        public TeamTextRomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public TeamTextDataConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("TeamTextDataConfig.id is required");

            return new TeamTextDataConfig(
                Id,
                Teams.Select(t => t.ToModel()).ToList(),
                Conferences.Select(c => c.ToModel()).ToList(),
                Divisions.Select(d => d.ToModel()).ToList(),
                DownNames.Select(d => d.ToModel()).ToList(),
                ControlTypes.Select(c => c.ToModel()).ToList(),
                OffenseDefenseLabels.ToModel(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class TeamTextEntryYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Mascot { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;

        public TeamTextEntry ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("TeamTextEntry.id is required");
            return new TeamTextEntry(
                Id,
                Abbreviation ?? string.Empty,
                City ?? string.Empty,
                Mascot ?? string.Empty,
                Conference ?? string.Empty,
                Division ?? string.Empty);
        }
    }

    private sealed class ConferenceTextYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public ConferenceText ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("ConferenceText.id is required");
            return new ConferenceText(Id, Abbreviation ?? string.Empty, FullName ?? string.Empty);
        }
    }

    private sealed class DivisionTextYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public DivisionText ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DivisionText.id is required");
            return new DivisionText(Id, Name ?? string.Empty);
        }
    }

    private sealed class DownNameYamlDto
    {
        public int Number { get; set; } = 0;
        public string Name { get; set; } = string.Empty;

        public DownName ToModel() => new(Number, Name ?? string.Empty);
    }

    private sealed class ControlTypeYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Short { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ControlType ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("ControlType.id is required");
            return new ControlType(Id, Short ?? string.Empty, Description ?? string.Empty);
        }
    }

    private sealed class OffenseDefenseLabelsYamlDto
    {
        public string Offense { get; set; } = "OFFENSE";
        public string Defense { get; set; } = "DEFENSE";

        public OffenseDefenseLabels ToModel() => new(Offense ?? "OFFENSE", Defense ?? "DEFENSE");
    }

    private sealed class TeamTextRomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int DataStart { get; set; } = 0xBC00;
        public int AbbreviationPointers { get; set; } = 0xBC00;
        public int CityPointers { get; set; } = 0xBC40;
        public int MascotPointers { get; set; } = 0xBC80;
        public int DownTextPointers { get; set; } = 0xBCA0;
        public int ControlTypePointers { get; set; } = 0xBCA8;

        public TeamTextRomInfo ToModel() => new(
            BaseAddress,
            DataStart,
            AbbreviationPointers,
            CityPointers,
            MascotPointers,
            DownTextPointers,
            ControlTypePointers);
    }
}
