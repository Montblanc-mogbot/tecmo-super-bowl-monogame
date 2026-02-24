using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FixedBankYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FixedBankConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FixedBankConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FixedBankConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public FixedBankConstantsYamlDto Constants { get; set; } = new();
        public SramCheckConfigYamlDto SramCheck { get; set; } = new();
        public ResetConfigYamlDto ResetConfig { get; set; } = new();
        public ChrBankDefaultsYamlDto ChrBankDefaults { get; set; } = new();
        public NmiConfigYamlDto NmiConfig { get; set; } = new();
        public IrqConfigYamlDto IrqConfig { get; set; } = new();
        public SoundRoutingConfigYamlDto SoundConfig { get; set; } = new();
        public List<SystemServiceYamlDto> ServiceTable { get; set; } = new();
        public RngConfigYamlDto RngConfig { get; set; } = new();
        public TaskSystemConfigYamlDto TaskSystem { get; set; } = new();
        public BufferSystemConfigYamlDto BufferSystem { get; set; } = new();
        public JoypadConfigYamlDto JoypadConfig { get; set; } = new();

        public FixedBankConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FixedBankConfig.id is required");

            return new FixedBankConfig(
                Id,
                Constants.ToModel(),
                SramCheck.ToModel(),
                ResetConfig.ToModel(),
                ChrBankDefaults.ToModel(),
                NmiConfig.ToModel(),
                IrqConfig.ToModel(),
                SoundConfig.ToModel(),
                ServiceTable.Select(s => s.ToModel()).ToList(),
                RngConfig.ToModel(),
                TaskSystem.ToModel(),
                BufferSystem.ToModel(),
                JoypadConfig.ToModel());
        }
    }

    private sealed class FixedBankConstantsYamlDto
    {
        public int VblanksToWaitAfterReset { get; set; } = 2;
        public int PrimeNumberForRandom1 { get; set; } = 131;
        public int PrimeNumberForRandom2 { get; set; } = 13;
        public int PrimeNumberForRandom3 { get; set; } = 17;
        public int PpuBusyBitflag { get; set; } = 128;

        public FixedBankConstants ToModel() => new(
            VblanksToWaitAfterReset,
            PrimeNumberForRandom1,
            PrimeNumberForRandom2,
            PrimeNumberForRandom3,
            PpuBusyBitflag);
    }

    private sealed class SramCheckConfigYamlDto
    {
        public string CheckValue { get; set; } = "AKIHIKO";
        public int ChecksumOffset { get; set; } = 7;

        public SramCheckConfig ToModel() => new(CheckValue, ChecksumOffset);
    }

    private sealed class ResetConfigYamlDto
    {
        public int DefaultStackIndex { get; set; } = 255;
        public int PpuWarmupVblanks { get; set; } = 2;
        public int SramClearStart { get; set; } = 24576;
        public int SramClearSize { get; set; } = 1691;

        public ResetConfig ToModel() => new(
            DefaultStackIndex,
            PpuWarmupVblanks,
            SramClearStart,
            SramClearSize);
    }

    private sealed class ChrBankDefaultsYamlDto
    {
        public int Bg0000Bank { get; set; } = 24;
        public int Spr1000Bank { get; set; } = 26;
        public int Spr1400Bank { get; set; } = 27;
        public int Spr1800Bank { get; set; } = 28;
        public int Spr1C00Bank { get; set; } = 29;

        public ChrBankDefaults ToModel() => new(
            Bg0000Bank,
            Spr1000Bank,
            Spr1400Bank,
            Spr1800Bank,
            Spr1C00Bank);
    }

    private sealed class NmiConfigYamlDto
    {
        public int PpuCtrlDefault { get; set; } = 168;
        public int PpuMaskDefault { get; set; } = 30;

        public NmiConfig ToModel() => new(PpuCtrlDefault, PpuMaskDefault);
    }

    private sealed class IrqConfigYamlDto
    {
        public int MaxSplits { get; set; } = 4;
        public int DefaultScanlines { get; set; } = 32;

        public IrqConfig ToModel() => new(MaxSplits, DefaultScanlines);
    }

    private sealed class SoundRoutingConfigYamlDto
    {
        public int EngineBank { get; set; } = 28;
        public int DataBank1 { get; set; } = 29;
        public int DataBank2 { get; set; } = 30;

        public SoundRoutingConfig ToModel() => new(EngineBank, DataBank1, DataBank2);
    }

    private sealed class SystemServiceYamlDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public SystemService ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidDataException("SystemService.name is required");
            return new SystemService(Name, Description);
        }
    }

    private sealed class RngConfigYamlDto
    {
        public int NumGenerators { get; set; } = 3;
        public string UpdateFrequency { get; set; } = "every_frame";

        public RngConfig ToModel() => new(NumGenerators, UpdateFrequency);
    }

    private sealed class TaskSystemConfigYamlDto
    {
        public int MaxTasks { get; set; } = 8;
        public List<TaskSlotYamlDto> TaskSlots { get; set; } = new();
        public List<string> TaskStates { get; set; } = new();

        public TaskSystemConfig ToModel() => new(
            MaxTasks,
            TaskSlots.Select(t => t.ToModel()).ToList(),
            TaskStates.ToList());
    }

    private sealed class TaskSlotYamlDto
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public int DefaultBank { get; set; } = 0;

        public TaskSlot ToModel() => new(Id, Name ?? string.Empty, DefaultBank);
    }

    private sealed class BufferSystemConfigYamlDto
    {
        public int BgBufferMaxLength { get; set; } = 255;
        public int SegmentOverhead { get; set; } = 3;

        public BufferSystemConfig ToModel() => new(BgBufferMaxLength, SegmentOverhead);
    }

    private sealed class JoypadConfigYamlDto
    {
        public int NumPlayers { get; set; } = 2;
        public List<string> Buttons { get; set; } = new();
        public string ReadMethod { get; set; } = "strobe_latch";

        public JoypadConfig ToModel() => new(NumPlayers, Buttons.ToList(), ReadMethod ?? "strobe_latch");
    }
}
