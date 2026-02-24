using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class PlayListYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PlayListConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<PlayListConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class PlayListConfigYamlDto
    {
        public List<PlayEntryYamlDto> PlayList { get; set; } = new();
        public List<SlotDefinitionYamlDto> Slots { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public PlayListConfig ToModel() => new(
            PlayList.Select(p => p.ToModel()).ToList(),
            Slots.Select(s => s.ToModel()).ToList(),
            Notes.ToList());
    }

    private sealed class PlayEntryYamlDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string Formation { get; set; } = string.Empty;
        public List<int> PlayNumbers { get; set; } = new();
        public List<string> Defense { get; set; } = new();

        public PlayEntry ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidDataException("PlayEntry.name is required");
            return new PlayEntry(
                Name,
                Slot ?? string.Empty,
                Formation ?? string.Empty,
                PlayNumbers.ToList(),
                Defense.ToList());
        }
    }

    private sealed class SlotDefinitionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public SlotDefinition ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SlotDefinition.id is required");
            return new SlotDefinition(Id, Name ?? string.Empty, Description ?? string.Empty);
        }
    }
}
