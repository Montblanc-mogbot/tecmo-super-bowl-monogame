using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class Bank9SpriteScriptYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static Bank9SpriteScriptConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<Bank9SpriteScriptConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class Bank9SpriteScriptConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SpriteOpcodeYamlDto> Opcodes { get; set; } = new();
        public List<SpriteScriptYamlDto> SpriteScripts { get; set; } = new();
        public List<TeamLogoSpriteYamlDto> TeamLogos { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public SpriteScriptRomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public Bank9SpriteScriptConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("Bank9SpriteScriptConfig.id is required");

            return new Bank9SpriteScriptConfig(
                Id,
                Opcodes.Select(o => o.ToModel()).ToList(),
                SpriteScripts.Select(s => s.ToModel()).ToList(),
                TeamLogos.Select(t => t.ToModel()).ToList(),
                Categories.ToList(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class SpriteOpcodeYamlDto
    {
        public int Code { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public List<string> Params { get; set; } = new();
        public string Description { get; set; } = string.Empty;

        public SpriteOpcode ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidDataException("SpriteOpcode.name is required");
            return new SpriteOpcode(Code, Name, Params.ToList(), Description ?? string.Empty);
        }
    }

    private sealed class SpriteScriptYamlDto
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public SpriteScript ToModel() => new(Id, Name ?? string.Empty, Description ?? string.Empty, Category ?? string.Empty);
    }

    private sealed class TeamLogoSpriteYamlDto
    {
        public int Id { get; set; } = 0;
        public string Team { get; set; } = string.Empty;

        public TeamLogoSprite ToModel() => new(Id, Team ?? string.Empty);
    }

    private sealed class SpriteScriptRomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int PointerTableStart { get; set; } = 0xA000;
        public int ScriptDataStart { get; set; } = 0xA100;
        public int NumScripts { get; set; } = 128;

        public SpriteScriptRomInfo ToModel() => new(BaseAddress, PointerTableStart, ScriptDataStart, NumScripts);
    }
}
