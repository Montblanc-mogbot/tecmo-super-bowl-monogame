using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class PlayCommandYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PlayCommandConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<PlayCommandConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class PlayCommandConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<PlayCommandDefYamlDto> Commands { get; set; } = new();
        public List<PlayCommandProgramYamlDto> Programs { get; set; } = new();

        public PlayCommandConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayCommandConfig.id is required");

            var cmdMap = new Dictionary<string, PlayCommandDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in Commands)
            {
                var model = c.ToModel();
                if (!cmdMap.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate play command id: {model.Id}");
            }

            var progMap = new Dictionary<string, PlayCommandProgram>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Programs)
            {
                var model = p.ToModel();
                if (!progMap.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate play command program id: {model.Id}");
            }

            // Light validation: each step must reference a known command.
            foreach (var prog in progMap.Values)
            {
                foreach (var step in prog.Steps)
                {
                    if (!cmdMap.ContainsKey(step.Command))
                        throw new InvalidDataException($"Program '{prog.Id}' references unknown command '{step.Command}'");
                }
            }

            return new PlayCommandConfig(Id, cmdMap, progMap);
        }
    }

    private sealed class PlayCommandDefYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public Dictionary<string, string>? Defaults { get; set; }

        public PlayCommandDef ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayCommandDef.id is required");

            if (string.IsNullOrWhiteSpace(Kind))
                throw new InvalidDataException($"PlayCommandDef.kind is required (command id '{Id}')");

            return new PlayCommandDef(
                Id,
                Kind,
                Defaults ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed class PlayCommandProgramYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<PlayCommandStepYamlDto> Steps { get; set; } = new();

        public PlayCommandProgram ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlayCommandProgram.id is required");

            return new PlayCommandProgram(
                Id,
                Steps.Select(s => s.ToModel()).ToList());
        }
    }

    private sealed class PlayCommandStepYamlDto
    {
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, string>? Args { get; set; }

        public PlayCommandStep ToModel()
        {
            if (string.IsNullOrWhiteSpace(Command))
                throw new InvalidDataException("PlayCommandStep.command is required");

            return new PlayCommandStep(
                Command,
                Args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
