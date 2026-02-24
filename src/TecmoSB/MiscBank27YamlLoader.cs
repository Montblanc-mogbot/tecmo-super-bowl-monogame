using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class MiscBank27YamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static MiscBank27Config LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<MiscBank27ConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class MiscBank27ConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, int>? IntConstants { get; set; }
        public Dictionary<string, string>? StringConstants { get; set; }
        public Dictionary<string, bool>? Flags { get; set; }

        public MiscBank27Config ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("MiscBank27Config.id is required");

            return new MiscBank27Config(
                Id,
                IntConstants ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                StringConstants ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Flags ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
