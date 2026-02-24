using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class SoundDataYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SoundDataConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<SoundDataConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class SoundDataConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SongDefYamlDto> Songs { get; set; } = new();
        public List<SfxDefYamlDto> Sfx { get; set; } = new();

        public SoundDataConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SoundDataConfig.id is required");

            return new SoundDataConfig(
                Id,
                Songs.Select(s => s.ToModel()).ToList(),
                Sfx.Select(s => s.ToModel()).ToList());
        }
    }

    private sealed class SongDefYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public int Tempo { get; set; } = 120;
        public List<SoundPatternYamlDto> Patterns { get; set; } = new();

        public SongDef ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SongDef.id is required");

            return new SongDef(Id, Tempo, Patterns.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class SfxDefYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SoundPatternYamlDto> Patterns { get; set; } = new();

        public SfxDef ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SfxDef.id is required");

            return new SfxDef(Id, Patterns.Select(p => p.ToModel()).ToList());
        }
    }

    private sealed class SoundPatternYamlDto
    {
        public string Channel { get; set; } = string.Empty;
        public List<SoundNoteYamlDto> Notes { get; set; } = new();

        public SoundPattern ToModel()
        {
            if (string.IsNullOrWhiteSpace(Channel))
                throw new InvalidDataException("SoundPattern.channel is required");

            return new SoundPattern(Channel, Notes.Select(n => n.ToModel()).ToList());
        }
    }

    private sealed class SoundNoteYamlDto
    {
        public string Pitch { get; set; } = string.Empty;
        public int Duration { get; set; } = 1;

        public SoundNote ToModel()
        {
            if (string.IsNullOrWhiteSpace(Pitch))
                throw new InvalidDataException("SoundNote.pitch is required");

            return new SoundNote(Pitch, Duration);
        }
    }
}
