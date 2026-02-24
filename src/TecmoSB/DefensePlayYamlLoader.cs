using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DefensePlayYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DefensePlayConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<DefensePlayConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class DefensePlayConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<DefensiveExecutionYamlDto> DefensiveExecutions { get; set; } = new();
        public List<DefensePlayerReactionYamlDto> DefensePlayerReactions { get; set; } = new();
        public List<SpecialTeamsExecutionYamlDto> SpecialTeamsExecutions { get; set; } = new();
        public DefenseRomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public DefensePlayConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DefensePlayConfig.id is required");

            return new DefensePlayConfig(
                Id,
                DefensiveExecutions.Select(e => e.ToModel()).ToList(),
                DefensePlayerReactions.Select(r => r.ToModel()).ToList(),
                SpecialTeamsExecutions.Select(s => s.ToModel()).ToList(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class DefensiveExecutionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PlayerReactionRefYamlDto> PlayerReactions { get; set; } = new();

        public DefensiveExecution ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DefensiveExecution.id is required");
            return new DefensiveExecution(
                Id,
                Description ?? string.Empty,
                PlayerReactions.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class DefensePlayerReactionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public DefensePlayerReaction ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DefensePlayerReaction.id is required");
            return new DefensePlayerReaction(Id, Description ?? string.Empty, Role ?? string.Empty);
        }
    }

    private sealed class SpecialTeamsExecutionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public SpecialTeamsExecution ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SpecialTeamsExecution.id is required");
            return new SpecialTeamsExecution(Id, Description ?? string.Empty);
        }
    }

    private sealed class DefenseRomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int DefensePointersStart { get; set; } = 0xA010;
        public int NumDefensiveExecutions { get; set; } = 100;

        public DefenseRomInfo ToModel() => new(BaseAddress, DefensePointersStart, NumDefensiveExecutions);
    }
}
