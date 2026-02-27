using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FormationDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FormationDataConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FormationDataConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FormationDataConfigYamlDto
    {
        public List<OffensiveFormationYamlDto> OffensiveFormations { get; set; } = new();

        // YAML is authored as a mapping:
        //   command_reference:
        //     SetPosFromKick:
        //       description: "..."
        //       params: [x, y]
        public Dictionary<string, CommandReferenceYamlDto> CommandReference { get; set; } = new();

        public List<FormationTypeYamlDto> FormationTypes { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public FormationDataConfig ToModel() => new(
            OffensiveFormations.Select(f => f.ToModel()).ToList(),
            CommandReference.Select(kvp => kvp.Value.ToModel(kvp.Key)).ToList(),
            FormationTypes.Select(t => t.ToModel()).ToList(),
            Notes.ToList());
    }

    private sealed class OffensiveFormationYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<FormationPlayerYamlDto> Players { get; set; } = new();

        public OffensiveFormation ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("OffensiveFormation.id is required");
            return new OffensiveFormation(
                Id,
                Name ?? string.Empty,
                Description ?? string.Empty,
                Players.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class FormationPlayerYamlDto
    {
        public string Position { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Commands { get; set; } = string.Empty;

        public FormationPlayer ToModel() => new(Position ?? string.Empty, Address ?? string.Empty, Commands ?? string.Empty);
    }

    private sealed class CommandReferenceYamlDto
    {
        public string Description { get; set; } = string.Empty;

        // YAML uses "params" for an array of parameter names.
        public List<string>? Params { get; set; }

        public CommandReference ToModel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("CommandReference key name is required");

            var param = Params is { Count: > 0 }
                ? string.Join(",", Params)
                : null;

            return new CommandReference(name, Description ?? string.Empty, param);
        }
    }

    private sealed class FormationTypeYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<string> FormationIds { get; set; } = new();

        public FormationType ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FormationType.id is required");
            return new FormationType(Id, FormationIds.ToList());
        }
    }
}
