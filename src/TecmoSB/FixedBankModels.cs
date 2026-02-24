namespace TecmoSB;

/// <summary>
/// YAML-driven scaffold for bank31 fixed bank.
///
/// Bank31 in the original ROM is the "fixed bank" at $C000-$FFFF that contains
/// the reset vector, NMI/IRQ handlers, and core system routines. We model the
/// configurable constants and service table as YAML data.
/// </summary>
public sealed record FixedBankConfig(
    string Id,
    FixedBankConstants Constants,
    SramCheckConfig SramCheck,
    ResetConfig ResetConfig,
    ChrBankDefaults ChrBankDefaults,
    NmiConfig NmiConfig,
    IrqConfig IrqConfig,
    SoundRoutingConfig SoundConfig,
    IReadOnlyList<SystemService> ServiceTable,
    RngConfig RngConfig,
    TaskSystemConfig TaskSystem,
    BufferSystemConfig BufferSystem,
    JoypadConfig JoypadConfig);

public sealed record FixedBankConstants(
    int VblanksToWaitAfterReset,
    int PrimeNumberForRandom1,
    int PrimeNumberForRandom2,
    int PrimeNumberForRandom3,
    int PpuBusyBitflag);

public sealed record SramCheckConfig(
    string CheckValue,
    int ChecksumOffset);

public sealed record ResetConfig(
    int DefaultStackIndex,
    int PpuWarmupVblanks,
    int SramClearStart,
    int SramClearSize);

public sealed record ChrBankDefaults(
    int Bg0000Bank,
    int Spr1000Bank,
    int Spr1400Bank,
    int Spr1800Bank,
    int Spr1C00Bank);

public sealed record NmiConfig(
    int PpuCtrlDefault,
    int PpuMaskDefault);

public sealed record IrqConfig(
    int MaxSplits,
    int DefaultScanlines);

public sealed record SoundRoutingConfig(
    int EngineBank,
    int DataBank1,
    int DataBank2);

public sealed record SystemService(
    string Name,
    string Description);

public sealed record RngConfig(
    int NumGenerators,
    string UpdateFrequency);

public sealed record TaskSystemConfig(
    int MaxTasks,
    IReadOnlyList<TaskSlot> TaskSlots,
    IReadOnlyList<string> TaskStates);

public sealed record TaskSlot(
    int Id,
    string Name,
    int DefaultBank);

public sealed record BufferSystemConfig(
    int BgBufferMaxLength,
    int SegmentOverhead);

public sealed record JoypadConfig(
    int NumPlayers,
    IReadOnlyList<string> Buttons,
    string ReadMethod);
