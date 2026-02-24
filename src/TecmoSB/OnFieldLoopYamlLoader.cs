using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class OnFieldLoopYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static OnFieldLoopConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<OnFieldLoopConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class OnFieldLoopConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string InitialState { get; set; } = string.Empty;
        public List<OnFieldStateYamlDto> States { get; set; } = new();

        public OnFieldLoopConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("OnFieldLoopConfig.id is required");

            if (string.IsNullOrWhiteSpace(InitialState))
                throw new InvalidDataException("OnFieldLoopConfig.initial_state is required");

            var stateMap = new Dictionary<string, OnFieldState>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in States)
            {
                var model = s.ToModel();
                if (!stateMap.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate on-field state id: {model.Id}");
            }

            if (!stateMap.ContainsKey(InitialState))
                throw new InvalidDataException($"initial_state '{InitialState}' not found in states");

            return new OnFieldLoopConfig(Id, InitialState, stateMap);
        }
    }

    private sealed class OnFieldStateYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Next { get; set; }
        public Dictionary<string, string>? OnEvent { get; set; }

        public OnFieldState ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("OnFieldState.id is required");

            if (string.IsNullOrWhiteSpace(Kind))
                throw new InvalidDataException($"OnFieldState.kind is required (state id '{Id}')");

            return new OnFieldState(
                Id,
                Kind,
                Next,
                OnEvent ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
