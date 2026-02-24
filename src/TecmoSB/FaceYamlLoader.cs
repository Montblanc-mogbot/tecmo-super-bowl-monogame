using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FaceYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FaceIndex LoadIndex(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FaceIndexDto>(yaml);

        var faces = dto.Faces.ToDictionary(
            kvp => ParseKey(kvp.Key),
            kvp => kvp.Value ?? string.Empty);

        return new FaceIndex(faces);
    }

    public static IReadOnlyList<FaceDef> LoadDefs(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FaceDefsDto>(yaml);
        return dto.Faces.Select(f => new FaceDef(f.Id, f.SpriteScriptId)).ToList();
    }

    private static int ParseKey(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(raw[2..], 16);
        }
        return int.Parse(raw);
    }

    private sealed class FaceIndexDto
    {
        public Dictionary<string, string?> Faces { get; set; } = new();
    }

    private sealed class FaceDefsDto
    {
        public List<FaceDefDto> Faces { get; set; } = new();
    }

    private sealed class FaceDefDto
    {
        public string Id { get; set; } = string.Empty;
        public string SpriteScriptId { get; set; } = string.Empty;
    }
}
