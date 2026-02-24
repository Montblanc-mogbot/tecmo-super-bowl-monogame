using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class MenuScriptYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static MenuScriptIndex LoadIndex(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<MenuIndexDto>(yaml);

        var scripts = dto.Scripts.ToDictionary(
            kvp => ParseKey(kvp.Key),
            kvp => kvp.Value ?? string.Empty);

        return new MenuScriptIndex(dto.Bank ?? "bank16", scripts);
    }

    public static MenuScript LoadScript(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<MenuScriptDto>(yaml);

        var ops = dto.Ops
            .Select(o => new MenuOp(o.Kind ?? string.Empty, o.Args ?? new Dictionary<string, string>()))
            .ToList();

        return new MenuScript(dto.Id ?? string.Empty, ops);
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

    private sealed class MenuIndexDto
    {
        public string? Bank { get; set; }
        public Dictionary<string, string?> Scripts { get; set; } = new();
    }

    private sealed class MenuScriptDto
    {
        public string? Id { get; set; }
        public List<MenuOpDto> Ops { get; set; } = new();
    }

    private sealed class MenuOpDto
    {
        public string? Kind { get; set; }
        public Dictionary<string, string>? Args { get; set; }
    }
}
