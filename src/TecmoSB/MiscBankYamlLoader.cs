using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class MiscBankYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static MiscBankConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<MiscBankConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class MiscBankConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, int>? IntConstants { get; set; }
        public Dictionary<string, string>? StringConstants { get; set; }
        public Dictionary<string, bool>? Flags { get; set; }

        public MiscBankConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("MiscBankConfig.id is required");

            return new MiscBankConfig(
                Id,
                IntConstants ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                StringConstants ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Flags ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
