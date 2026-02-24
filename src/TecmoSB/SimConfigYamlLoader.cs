using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class SimConfigYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SimConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<SimConfigDto>(yaml);

        return new SimConfig(
            dto.MaxScoreLimit,
            dto.YardsForFirstDown,
            dto.MinutesPerQuarter,
            dto.LengthOfFieldYards,
            dto.XpKickDistanceYards);
    }

    private sealed class SimConfigDto
    {
        public int MaxScoreLimit { get; set; }
        public int YardsForFirstDown { get; set; }
        public int MinutesPerQuarter { get; set; }
        public int LengthOfFieldYards { get; set; }
        public int XpKickDistanceYards { get; set; }
    }
}
