using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class DmcSamplesYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DmcSamplesConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<DmcSamplesConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class DmcSamplesConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<DmcSampleYamlDto> Samples { get; set; } = new();
        public DmcPlaybackConfigYamlDto DmcConfig { get; set; } = new();
        public ResetVectorConfigYamlDto ResetVector { get; set; } = new();
        public AssetMappingConfigYamlDto AssetMapping { get; set; } = new();

        public DmcSamplesConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DmcSamplesConfig.id is required");

            return new DmcSamplesConfig(
                Id,
                Samples.Select(s => s.ToModel()).ToList(),
                DmcConfig.ToModel(),
                ResetVector.ToModel(),
                AssetMapping.ToModel());
        }
    }

    private sealed class DmcSampleYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int RomOffset { get; set; } = 0;
        public int Length { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public string? Note { get; set; }

        public DmcSample ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("DmcSample.id is required");
            return new DmcSample(Id, Name ?? string.Empty, RomOffset, Length, Description ?? string.Empty, Note);
        }
    }

    private sealed class DmcPlaybackConfigYamlDto
    {
        public List<int> PlaybackRates { get; set; } = new();
        public List<KickPitchVariantYamlDto> KickPitchVariants { get; set; } = new();

        public DmcPlaybackConfig ToModel() => new(
            PlaybackRates.ToList(),
            KickPitchVariants.Select(k => k.ToModel()).ToList());
    }

    private sealed class KickPitchVariantYamlDto
    {
        public int RateIndex { get; set; } = 0;
        public string Name { get; set; } = string.Empty;

        public KickPitchVariant ToModel() => new(RateIndex, Name ?? string.Empty);
    }

    private sealed class ResetVectorConfigYamlDto
    {
        public int BaseAddress { get; set; } = 0xE000;
        public int PadAddress { get; set; } = 0xFFF0;
        public VectorTableYamlDto Vectors { get; set; } = new();

        public ResetVectorConfig ToModel() => new(BaseAddress, PadAddress, Vectors.ToModel());
    }

    private sealed class VectorTableYamlDto
    {
        public string Nmi { get; set; } = "NMI_START";
        public string Reset { get; set; } = "RESET_START";
        public string Irq { get; set; } = "IRQ_START";

        public VectorTable ToModel() => new(Nmi ?? "NMI_START", Reset ?? "RESET_START", Irq ?? "IRQ_START");
    }

    private sealed class AssetMappingConfigYamlDto
    {
        public string Format { get; set; } = "wav";
        public string Directory { get; set; } = "Audio/DMC";
        public List<AssetMappingYamlDto> Samples { get; set; } = new();

        public AssetMappingConfig ToModel() => new(
            Format ?? "wav",
            Directory ?? "Audio/DMC",
            Samples.Select(s => s.ToModel()).ToList());
    }

    private sealed class AssetMappingYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;

        public AssetMapping ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("AssetMapping.id is required");
            return new AssetMapping(Id, File ?? string.Empty);
        }
    }
}
