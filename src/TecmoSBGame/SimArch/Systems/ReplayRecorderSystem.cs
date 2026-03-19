using Arch.Core;
using TecmoSBGame.SimArch.Replay;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Records deterministic per-tick replay frames.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ReplayRecorderSystem.cs
/// </summary>
public sealed class ReplayRecorderSystem
{
    private readonly ReplayRecorder _recorder;
    private readonly PlayState _play;

    public ReplayRecorderSystem(ReplayRecorder recorder, PlayState play)
    {
        _recorder = recorder;
        _play = play;
    }

    public void Update(World world)
    {
        // TODO: capture entity positions + ball state into ReplayRecorder.Capture.
        _ = world;
        _ = _play;
    }
}
