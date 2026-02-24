using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DefPlayListPointerYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DefPlayListPointerConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<DefPlayListPointerConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class DefPlayListPointerConfigYamlDto
    {
        public List<DefensivePlayPointerYamlDto> DefensivePlayPointers { get; set; } = new();
        public Dictionary<string, string> ReferenceMappings { get; set; } = new();
        public PlayerPositionMappingYamlDto PlayerPositions { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public DefPlayListPointerConfig ToModel() => new(
            DefensivePlayPointers.Select(p => p.ToModel()).ToList(),
            ReferenceMappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            PlayerPositions.ToModel(),
            Notes.ToList());
    }

    private sealed class DefensivePlayPointerYamlDto
    {
        public int PlayId { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public List<int> PlayerReactions { get; set; } = new();
        public List<int>? ReferenceIds { get; set; }

        public DefensivePlayPointer ToModel() => new(PlayId, Description ?? string.Empty, PlayerReactions.ToList(), ReferenceIds?.ToList());
    }

    private sealed class PlayerPositionMappingYamlDto
    {
        public string Index0 { get; set; } = "RE";
        public string Index1 { get; set; } = "NT";
        public string Index2 { get; set; } = "LE";
        public string Index3 { get; set; } = "ROLB";
        public string Index4 { get; set; } = "RILB";
        public string Index5 { get; set; } = "LILB";
        public string Index6 { get; set; } = "LOLB";
        public string Index7 { get; set; } = "RCB";
        public string Index8 { get; set; } = "LCB";
        public string Index9 { get; set; } = "FS";
        public string Index10 { get; set; } = "SS";

        public PlayerPositionMapping ToModel() => new(Index0, Index1, Index2, Index3, Index4, Index5, Index6, Index7, Index8, Index9, Index10);
    }
}
