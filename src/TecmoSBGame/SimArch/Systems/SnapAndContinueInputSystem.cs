using System;
using Arch.Core;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Converts UI button edges into high-level lifecycle events:
/// - SnapEvent during PreSnap
/// - PostPlayContinueRequestedEvent during PostPlay
///
/// Keeps MainGame as input provider only.
/// </summary>
public sealed class SnapAndContinueInputSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;

    public SnapAndContinueInputSystem(MatchState match, PlayState play)
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public void Update(World world, in UiButtons ui)
    {
        _ = world;

        if (_play.Phase == PlayPhase.PreSnap && ui.Snap)
        {
            var offenseTeam = _match.KickoffPending
                ? _match.ReceivingTeamIndex
                : _match.FieldGoalPending
                    ? _match.PossessionTeam
                    : _match.PossessionTeam;
            var defenseTeam = _match.KickoffPending
                ? _match.KickingTeamIndex
                : 1 - _match.PossessionTeam;
            var snap = new SnapEvent(OffenseTeam: offenseTeam, DefenseTeam: defenseTeam);
            SimEventBus.Send(ref snap);
        }

        if (_play.Phase == PlayPhase.PostPlay && ui.Continue)
        {
            var cont = new PostPlayContinueRequestedEvent(_play.PlayId);
            SimEventBus.Send(ref cont);
        }
    }
}
