using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class BgMetatileYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static IReadOnlyList<BgMetatile> LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<MetatileSetDto>(yaml);
        return dto.Metatiles.Select(m => m.ToModel()).ToList();
    }

    private sealed class MetatileSetDto
    {
        public string? Bank { get; set; }
        public List<BgMetatileDto> Metatiles { get; set; } = new();
    }

    private sealed class BgMetatileDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Attr { get; set; }
        public List<List<string>> Tiles { get; set; } = new();

        public BgMetatile ToModel()
        {
            if (Tiles.Count != 4 || Tiles.Any(r => r.Count != 4))
            {
                throw new InvalidOperationException($"BgMetatile '{Id}' must be 4 rows x 4 cols");
            }

            var attr = ParseInt(Attr ?? "0x00");
            var grid = Tiles
                .Select(r => r.Select(ParseInt).ToArray())
                .ToArray();

            return new BgMetatile(Id, attr, grid);
        }

        private static int ParseInt(string raw)
        {
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt32(raw[2..], 16);
            }
            return int.Parse(raw);
        }
    }
}
