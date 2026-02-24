using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class LeadersYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static LeadersConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<LeadersConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class LeadersConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<LeaderCategoryYamlDto> Categories { get; set; } = new();
        public Dictionary<string, string>? ProBowlAbbrevs { get; set; }

        public LeadersConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("LeadersConfig.id is required");

            return new LeadersConfig(
                Id,
                Categories.Select(c => c.ToModel()).ToList(),
                ProBowlAbbrevs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed class LeaderCategoryYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StatKey { get; set; } = string.Empty;
        public int TopN { get; set; } = 10;

        public LeaderCategory ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("LeaderCategory.id is required");

            if (string.IsNullOrWhiteSpace(StatKey))
                throw new InvalidDataException($"LeaderCategory.stat_key is required (category id '{Id}')");

            return new LeaderCategory(Id, Title, StatKey, TopN);
        }
    }
}
