using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class TeamDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static TeamDataConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<TeamDataConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class TeamDataConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<TeamDefinitionYamlDto> Teams { get; set; } = new();

        public TeamDataConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("TeamDataConfig.id is required");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in Teams)
            {
                if (!seen.Add(t.Id))
                    throw new InvalidDataException($"Duplicate team id: {t.Id}");
            }

            return new TeamDataConfig(Id, Teams.Select(t => t.ToModel()).ToList());
        }
    }

    private sealed class TeamDefinitionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Abbrev { get; set; } = string.Empty;
        public TeamColorsYamlDto Colors { get; set; } = new();

        public TeamDefinition ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("TeamDefinition.id is required");

            if (string.IsNullOrWhiteSpace(Abbrev))
                throw new InvalidDataException($"TeamDefinition.abbrev is required (team id '{Id}')");

            return new TeamDefinition(Id, City, Name, Abbrev, Colors.ToModel());
        }
    }

    private sealed class TeamColorsYamlDto
    {
        public string Primary { get; set; } = string.Empty;
        public string Secondary { get; set; } = string.Empty;
        public string? Accent { get; set; }

        public TeamColors ToModel()
        {
            if (string.IsNullOrWhiteSpace(Primary) || string.IsNullOrWhiteSpace(Secondary))
                throw new InvalidDataException("TeamColors.primary and TeamColors.secondary are required");

            return new TeamColors(Primary, Secondary, Accent);
        }
    }
}
