using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class SoundEngineYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SoundEngineConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<SoundEngineConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class SoundEngineConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SoundDefYamlDto> Sounds { get; set; } = new();
        public Dictionary<string, string>? EventMap { get; set; }

        public SoundEngineConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SoundEngineConfig.id is required");

            var soundMap = new Dictionary<string, SoundDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Sounds)
            {
                var model = s.ToModel();
                if (!soundMap.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate sound id: {model.Id}");
            }

            // Light validation: eventMap values should reference defined sounds
            var em = EventMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (evt, soundId) in em)
            {
                if (!soundMap.ContainsKey(soundId))
                    throw new InvalidDataException($"Event '{evt}' maps to unknown sound '{soundId}'");
            }

            return new SoundEngineConfig(Id, soundMap, em);
        }
    }

    private sealed class SoundDefYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "sfx";
        public string Asset { get; set; } = string.Empty;
        public float Volume { get; set; } = 1.0f;
        public bool Loop { get; set; }

        public SoundDef ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SoundDef.id is required");

            if (string.IsNullOrWhiteSpace(Asset))
                throw new InvalidDataException($"SoundDef.asset is required (sound id '{Id}')");

            return new SoundDef(Id, Kind, Asset, Volume, Loop);
        }
    }
}
