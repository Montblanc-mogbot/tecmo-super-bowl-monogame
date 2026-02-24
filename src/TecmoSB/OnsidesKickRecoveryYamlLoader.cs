using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class OnsidesKickRecoveryYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static OnsidesKickRecoveryConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<OnsidesKickRecoveryConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class OnsidesKickRecoveryConfigYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public string BugExplanation { get; set; } = string.Empty;
        public List<TeamRecoveryDataYamlDto> TeamRecoveryData { get; set; } = new();
        public List<PowerBarLevelYamlDto> PowerBarLevels { get; set; } = new();
        public string RecoveryMechanics { get; set; } = string.Empty;
        public string FixRecommendation { get; set; } = string.Empty;
        public string DataFormat { get; set; } = string.Empty;
        public List<string> Notes { get; set; } = new();

        public OnsidesKickRecoveryConfig ToModel() => new(
            Description ?? string.Empty,
            BugExplanation ?? string.Empty,
            TeamRecoveryData.Select(t => t.ToModel()).ToList(),
            PowerBarLevels.Select(p => p.ToModel()).ToList(),
            RecoveryMechanics ?? string.Empty,
            FixRecommendation ?? string.Empty,
            DataFormat ?? string.Empty,
            Notes.ToList());
    }

    private sealed class TeamRecoveryDataYamlDto
    {
        public string Team { get; set; } = string.Empty;
        public string P1RecoveryHex { get; set; } = string.Empty;
        public string P2RecoveryHex { get; set; } = string.Empty;

        public TeamRecoveryData ToModel() => new(Team ?? string.Empty, P1RecoveryHex ?? string.Empty, P2RecoveryHex ?? string.Empty);
    }

    private sealed class PowerBarLevelYamlDto
    {
        public string Level { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? P1Distance { get; set; }
        public int? P2Distance { get; set; }
        public bool? BugAffected { get; set; }

        public PowerBarLevel ToModel() => new(Level ?? string.Empty, Description ?? string.Empty, P1Distance, P2Distance, BugAffected);
    }
}
