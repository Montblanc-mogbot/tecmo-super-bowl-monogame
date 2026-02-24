using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FgWorksheetYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FgWorksheetConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FgWorksheetConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FgWorksheetConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<LosToFgDistanceYamlDto> DistanceMultipliers { get; set; } = new();
        public RandomModifierConfigYamlDto RandomModifier { get; set; } = new();
        public MaxArrowRangeConfigYamlDto MaxArrowRange { get; set; } = new();
        public SuccessLookupTableYamlDto SuccessLookupTable { get; set; } = new();
        public NotchThresholdsConfigYamlDto NotchThresholds { get; set; } = new();
        public AutoTapConfigYamlDto AutoTap { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public FgWorksheetConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FgWorksheetConfig.id is required");

            return new FgWorksheetConfig(
                Id,
                DistanceMultipliers.Select(d => d.ToModel()).ToList(),
                RandomModifier.ToModel(),
                MaxArrowRange.ToModel(),
                SuccessLookupTable.ToModel(),
                NotchThresholds.ToModel(),
                AutoTap.ToModel(),
                Notes.ToList());
        }
    }

    private sealed class LosToFgDistanceYamlDto
    {
        public int Los { get; set; } = 0;
        public int FgDistance { get; set; } = 0;
        public double Multiplier { get; set; } = 0;

        public LosToFgDistance ToModel() => new(Los, FgDistance, Multiplier);
    }

    private sealed class RandomModifierConfigYamlDto
    {
        public List<int> Values { get; set; } = new();
        public double ProbabilityEach { get; set; } = 0.25;
        public bool Special50KaKicker { get; set; } = true;

        public RandomModifierConfig ToModel() => new(Values.ToList(), ProbabilityEach, Special50KaKicker);
    }

    private sealed class MaxArrowRangeConfigYamlDto
    {
        public string Formula { get; set; } = string.Empty;

        public MaxArrowRangeConfig ToModel() => new(Formula ?? string.Empty);
    }

    private sealed class SuccessLookupTableYamlDto
    {
        public List<SuccessLookupEntryYamlDto> Entries { get; set; } = new();

        public SuccessLookupTable ToModel() => new(Entries.Select(e => e.ToModel()).ToList());
    }

    private sealed class SuccessLookupEntryYamlDto
    {
        public int Index { get; set; } = 0;
        public int Los { get; set; } = 0;
        public int FgDistance { get; set; } = 0;
        public string Multiplier { get; set; } = string.Empty;
        public string ResultGood { get; set; } = string.Empty;
        public string ResultMiss { get; set; } = string.Empty;
        public int MaxNotchesPerfect { get; set; } = 0;
        public int MaxNotchesGood { get; set; } = 0;
        public int MaxNotchesTotal { get; set; } = 0;

        public SuccessLookupEntry ToModel() => new(
            Index,
            Los,
            FgDistance,
            Multiplier ?? string.Empty,
            ResultGood ?? string.Empty,
            ResultMiss ?? string.Empty,
            MaxNotchesPerfect,
            MaxNotchesGood,
            MaxNotchesTotal);
    }

    private sealed class NotchThresholdsConfigYamlDto
    {
        public string PerfectCenter { get; set; } = string.Empty;
        public string PerDistanceIncrease { get; set; } = string.Empty;

        public NotchThresholdsConfig ToModel() => new(
            PerfectCenter ?? string.Empty,
            PerDistanceIncrease ?? string.Empty);
    }

    private sealed class AutoTapConfigYamlDto
    {
        public bool Enabled { get; set; } = true;
        public int LosThreshold { get; set; } = 22;
        public string Description { get; set; } = string.Empty;

        public AutoTapConfig ToModel() => new(Enabled, LosThreshold, Description ?? string.Empty);
    }
}
