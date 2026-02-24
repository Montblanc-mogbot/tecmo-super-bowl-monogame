using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class PlaycallYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PlaycallConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<PlaycallConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class PlaycallConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<PlaycallScreenYamlDto> Screens { get; set; } = new();

        public PlaycallConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlaycallConfig.id is required");

            var map = new Dictionary<string, PlaycallScreen>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Screens)
            {
                var model = s.ToModel();
                if (!map.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate playcall screen id: {model.Id}");
            }

            return new PlaycallConfig(Id, map);
        }
    }

    private sealed class PlaycallScreenYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<PlaycallOptionYamlDto> Options { get; set; } = new();

        public PlaycallScreen ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlaycallScreen.id is required");

            return new PlaycallScreen(
                Id,
                Title,
                Options.Select(o => o.ToModel()).ToList());
        }
    }

    private sealed class PlaycallOptionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? NextScreen { get; set; }
        public string? EmitEvent { get; set; }

        public PlaycallOption ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("PlaycallOption.id is required");

            if (string.IsNullOrWhiteSpace(Label))
                throw new InvalidDataException($"PlaycallOption.label is required (option id '{Id}')");

            return new PlaycallOption(Id, Label, NextScreen, EmitEvent);
        }
    }
}
