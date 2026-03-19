using Arch.Core;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Consumes SnapEvent and transitions PlayState from PreSnap to InPlay.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/SnapResolutionSystem.cs
/// </summary>
public sealed class SnapResolutionSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;

    public SnapResolutionSystem(MatchState match, PlayState play)
    {
        _match = match;
        _play = play;
    }

    public void Update(World world)
    {
        _ = world;

        foreach (var e in SimEventBus.Drain<SnapEvent>())
        {
            _match.PossessionTeam = e.OffenseTeam;
            _play.Phase = PlayPhase.InPlay;
        }
    }
}
