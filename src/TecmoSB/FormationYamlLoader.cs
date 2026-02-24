using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FormationYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FormationConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FormationConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FormationConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<string> FormationTypes { get; set; } = new();
        public List<FormationPointerSetYamlDto> FormationPointers { get; set; } = new();
        public List<PlayerReactionYamlDto> PlayerReactions { get; set; } = new();
        public MetatileConfigYamlDto MetatileConfig { get; set; } = new();
        public RomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public FormationConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FormationConfig.id is required");

            return new FormationConfig(
                Id,
                FormationTypes.ToList(),
                FormationPointers.Select(f => f.ToModel()).ToList(),
                PlayerReactions.Select(p => p.ToModel()).ToList(),
                MetatileConfig.ToModel(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class FormationPointerSetYamlDto
    {
        public string Formation { get; set; } = string.Empty;
        public int TypeId { get; set; } = 0;
        public List<PlayerReactionRefYamlDto> PlayerReactions { get; set; } = new();

        public FormationPointerSet ToModel()
        {
            if (string.IsNullOrWhiteSpace(Formation))
                throw new InvalidDataException("FormationPointerSet.formation is required");
            return new FormationPointerSet(
                Formation,
                TypeId,
                PlayerReactions.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class PlayerReactionRefYamlDto
    {
        public int Index { get; set; } = 0;
        public string ReactionId { get; set; } = string.Empty;

        public PlayerReactionRef ToModel() => new(Index, ReactionId ?? string.Empty);
    }

    private sealed class PlayerReactionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PlayerReaction ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayerReaction.id is required");
            return new PlayerReaction(Id, Description ?? string.Empty);
        }
    }

    private sealed class MetatileConfigYamlDto
    {
        public int TileSize { get; set; } = 8;
        public int MetatileSize { get; set; } = 16;
        public List<string> Categories { get; set; } = new();

        public MetatileConfig ToModel() => new(TileSize, MetatileSize, Categories.ToList());
    }

    private sealed class RomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int FormationPointersStart { get; set; } = 0xA010;
        public int FormationDataStart { get; set; } = 0xA043;

        public RomInfo ToModel() => new(BaseAddress, FormationPointersStart, FormationDataStart);
    }
}
