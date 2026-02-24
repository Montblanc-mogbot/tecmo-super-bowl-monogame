using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

/// <summary>
/// Loads banked sprite scripts from YAML (index + per-script YAML files).
///
/// Bank indices live in: content/spritescripts/banks/<bank>/index.yaml
/// Script YAMLs live in:  content/spritescripts/<scriptId>.yaml
/// </summary>
public sealed class SpriteScriptBankRepository
{
    private readonly string _contentRoot;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public SpriteScriptBankRepository(string contentRoot)
    {
        _contentRoot = contentRoot;
    }

    public SpriteScriptBankIndex LoadBankIndex(string bank)
    {
        var path = Path.Combine(_contentRoot, "spritescripts", "banks", bank, "index.yaml");
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<BankIndexDto>(yaml);

        var scripts = dto.Scripts
            .ToDictionary(
                kvp => ParseKey(kvp.Key),
                kvp => kvp.Value ?? string.Empty);

        return new SpriteScriptBankIndex(dto.Bank ?? bank, scripts);
    }

    public SpriteScript LoadScript(string scriptId)
    {
        var path = Path.Combine(_contentRoot, "spritescripts", $"{scriptId}.yaml");
        return SpriteScriptYamlLoader.LoadFromFile(path);
    }

    public SpriteScript LoadScriptFromBank(string bank, int code)
    {
        var idx = LoadBankIndex(bank);
        if (!idx.Scripts.TryGetValue(code, out var id) || string.IsNullOrWhiteSpace(id))
        {
            throw new KeyNotFoundException($"No sprite script mapping for bank={bank} code=0x{code:X2}");
        }

        return LoadScript(id);
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

    private sealed class BankIndexDto
    {
        public string? Bank { get; set; }
        public Dictionary<string, string?> Scripts { get; set; } = new();
    }
}
