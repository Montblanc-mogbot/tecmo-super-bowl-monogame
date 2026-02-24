using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class PlayDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PlayDataConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<PlayDataConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class PlayDataConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<PlayCommandTypeYamlDto> CommandTypes { get; set; } = new();
        public List<PlayerReactionScriptYamlDto> PlayerReactions { get; set; } = new();
        public List<PlayCategoryYamlDto> Categories { get; set; } = new();
        public PlayDataRomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public PlayDataConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayDataConfig.id is required");

            return new PlayDataConfig(
                Id,
                CommandTypes.Select(c => c.ToModel()).ToList(),
                PlayerReactions.Select(r => r.ToModel()).ToList(),
                Categories.Select(c => c.ToModel()).ToList(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class PlayCommandTypeYamlDto
    {
        public string Name { get; set; } = string.Empty;
        public int Opcode { get; set; } = 0;
        public List<string> Params { get; set; } = new();
        public string Description { get; set; } = string.Empty;

        public PlayCommandType ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidDataException("PlayCommandType.name is required");
            return new PlayCommandType(Name, Opcode, Params.ToList(), Description ?? string.Empty);
        }
    }

    private sealed class PlayerReactionScriptYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<PlayCommandYamlDto> Commands { get; set; } = new();

        public PlayerReactionScript ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayerReactionScript.id is required");
            return new PlayerReactionScript(
                Id,
                Description ?? string.Empty,
                Role ?? string.Empty,
                Commands.Select(c => c.ToModel()).ToList());
        }
    }

    private sealed class PlayCommandYamlDto
    {
        public string Cmd { get; set; } = string.Empty;
        public List<object>? Params { get; set; }
        public string? Target { get; set; }
        public string? Label { get; set; }

        public PlayCommand ToModel()
        {
            if (string.IsNullOrWhiteSpace(Cmd))
                throw new InvalidDataException("PlayCommand.cmd is required");
            return new PlayCommand(Cmd, Params?.ToList(), Target, Label);
        }
    }

    private sealed class PlayCategoryYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<int> Reactions { get; set; } = new();

        public PlayCategory ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayCategory.id is required");
            return new PlayCategory(Id, Description ?? string.Empty, Reactions.ToList());
        }
    }

    private sealed class PlayDataRomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int OffenseDataStart { get; set; } = 0xA010;
        public int DefenseDataStart { get; set; } = 0xB800;
        public int TotalReactions { get; set; } = 500;

        public PlayDataRomInfo ToModel() => new(BaseAddress, OffenseDataStart, DefenseDataStart, TotalReactions);
    }
}
