using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DrawScriptYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DrawScript LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<DrawScriptYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class DrawScriptYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<DrawOpYamlDto> Ops { get; set; } = new();

        public DrawScript ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DrawScript.id is required");

            return new DrawScript(
                Id,
                Ops.Select(o => o.ToModel()).ToList());
        }
    }

    private sealed class DrawOpYamlDto
    {
        public string Kind { get; set; } = string.Empty;
        public Dictionary<string, string>? Args { get; set; }

        public DrawOp ToModel()
        {
            if (string.IsNullOrWhiteSpace(Kind))
                throw new InvalidDataException("DrawOp.kind is required");

            return new DrawOp(
                Kind,
                Args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
