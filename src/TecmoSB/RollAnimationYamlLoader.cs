using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class RollAnimationYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static RollAnimationConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<RollAnimationConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class RollAnimationConfigYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public List<RomLocationYamlDto> RomLocations { get; set; } = new();
        public List<AnimationPropertyYamlDto> AnimationProperties { get; set; } = new();
        public string MonogameNotes { get; set; } = string.Empty;
        public List<string> Notes { get; set; } = new();

        public RollAnimationConfig ToModel() => new(
            Description ?? string.Empty,
            RomLocations.Select(r => r.ToModel()).ToList(),
            AnimationProperties.Select(a => a.ToModel()).ToList(),
            MonogameNotes ?? string.Empty,
            Notes.ToList());
    }

    private sealed class RomLocationYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public RomLocation ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("RomLocation.id is required");
            return new RomLocation(Id, Description ?? string.Empty);
        }
    }

    private sealed class AnimationPropertyYamlDto
    {
        public string Property { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public AnimationProperty ToModel() => new(Property ?? string.Empty, Description ?? string.Empty);
    }
}
