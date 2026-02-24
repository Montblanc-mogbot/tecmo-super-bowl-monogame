using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class SceneScriptYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SceneScriptConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<SceneScriptConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class SceneScriptConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SceneOpcodeYamlDto> Opcodes { get; set; } = new();
        public List<string> SceneTypes { get; set; } = new();
        public List<SceneScriptYamlDto> SceneScripts { get; set; } = new();
        public SceneScriptRomInfoYamlDto RomInfo { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public SceneScriptConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SceneScriptConfig.id is required");

            return new SceneScriptConfig(
                Id,
                Opcodes.Select(o => o.ToModel()).ToList(),
                SceneTypes.ToList(),
                SceneScripts.Select(s => s.ToModel()).ToList(),
                RomInfo.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class SceneOpcodeYamlDto
    {
        public int Code { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public List<string> Params { get; set; } = new();
        public string Description { get; set; } = string.Empty;

        public SceneOpcode ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidDataException("SceneOpcode.name is required");
            return new SceneOpcode(Code, Name, Params.ToList(), Description ?? string.Empty);
        }
    }

    private sealed class SceneScriptYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<SceneCommandYamlDto> Commands { get; set; } = new();

        public SceneScript ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SceneScript.id is required");
            return new SceneScript(Id, Description ?? string.Empty, Commands.Select(c => c.ToModel()).ToList());
        }
    }

    private sealed class SceneCommandYamlDto
    {
        public string Opcode { get; set; } = string.Empty;
        public List<int>? Params { get; set; }

        public SceneCommand ToModel() => new(Opcode ?? string.Empty, Params?.ToList());
    }

    private sealed class SceneScriptRomInfoYamlDto
    {
        public int BaseAddress { get; set; } = 0xA000;
        public int MacroSection { get; set; } = 0xA000;
        public int ScriptDataStart { get; set; } = 0xB000;

        public SceneScriptRomInfo ToModel() => new(BaseAddress, MacroSection, ScriptDataStart);
    }
}
