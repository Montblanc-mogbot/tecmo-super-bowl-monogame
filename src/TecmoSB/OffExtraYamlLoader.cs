using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class OffExtraYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static OffExtraConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<OffExtraConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class OffExtraConfigYamlDto
    {
        public List<OffensiveExtraPlayYamlDto> OffensiveExtraPlays { get; set; } = new();
        public List<CommandReferenceYamlDto> CommandReference { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public OffExtraConfig ToModel() => new(
            OffensiveExtraPlays.Select(p => p.ToModel()).ToList(),
            CommandReference.Select(c => c.ToModel()).ToList(),
            Notes.ToList());
    }

    private sealed class OffensiveExtraPlayYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<OffensivePlayerCommandYamlDto> Players { get; set; } = new();

        public OffensiveExtraPlay ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("OffensiveExtraPlay.id is required");
            return new OffensiveExtraPlay(
                Id,
                Name ?? string.Empty,
                Description ?? string.Empty,
                Players.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class OffensivePlayerCommandYamlDto
    {
        public string Position { get; set; } = string.Empty;
        public string Commands { get; set; } = string.Empty;

        public OffensivePlayerCommand ToModel() => new(Position ?? string.Empty, Commands ?? string.Empty);
    }
}
