using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class YardlineReferenceYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static YardlineReferenceConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<YardlineReferenceConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class YardlineReferenceConfigYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public List<YardlineEntryYamlDto> Yardlines { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public YardlineReferenceConfig ToModel() => new(
            Description ?? string.Empty,
            Yardlines.Select(y => y.ToModel()).ToList(),
            Notes.ToList());
    }

    private sealed class YardlineEntryYamlDto
    {
        public int Yard { get; set; } = 0;
        public int Hb { get; set; } = 0;
        public int Lb { get; set; } = 0;
        public string TwoByte { get; set; } = string.Empty;

        public YardlineEntry ToModel() => new(Yard, Hb, Lb, TwoByte ?? string.Empty);
    }
}
