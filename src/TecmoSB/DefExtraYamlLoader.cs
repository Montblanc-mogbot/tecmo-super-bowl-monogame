using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DefExtraYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DefExtraConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<DefExtraConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class DefExtraConfigYamlDto
    {
        public List<DefensiveExtraPlayYamlDto> DefensiveExtraPlays { get; set; } = new();
        public List<CommandReferenceYamlDto> CommandReference { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public DefExtraConfig ToModel() => new(
            DefensiveExtraPlays.Select(p => p.ToModel()).ToList(),
            CommandReference.Select(c => c.ToModel()).ToList(),
            Notes.ToList());
    }

    private sealed class DefensiveExtraPlayYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DefensivePlayerCommandYamlDto> Players { get; set; } = new();

        public DefensiveExtraPlay ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DefensiveExtraPlay.id is required");
            return new DefensiveExtraPlay(
                Id,
                Name ?? string.Empty,
                Description ?? string.Empty,
                Players.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class DefensivePlayerCommandYamlDto
    {
        public string Position { get; set; } = string.Empty;
        public string Commands { get; set; } = string.Empty;

        public DefensivePlayerCommand ToModel() => new(Position ?? string.Empty, Commands ?? string.Empty);
    }

    private sealed class CommandReferenceYamlDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Param { get; set; }

        public CommandReference ToModel() => new(Name ?? string.Empty, Description ?? string.Empty, Param);
    }
}
