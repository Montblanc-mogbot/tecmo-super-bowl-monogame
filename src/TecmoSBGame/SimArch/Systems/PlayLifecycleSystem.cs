using System;
using Arch.Core;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Play lifecycle state machine (SimArch).
///
/// Drives: PreSnap → InPlay → PostPlay → PreSnap
/// strictly via events:
/// - PlaySelectedEvent (new play selected)
/// - SnapEvent (snap occurs)
/// - PlayEndedEvent (play ends)
/// - PostPlayContinueRequestedEvent (user advances)
///
/// Host UI should feed inputs (play selection, snap, continue) and render based on PlayState.Phase.
/// </summary>
public sealed class PlayLifecycleSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly KickoffAfterScoreSystem? _kickoffAfterScore;
    private int _lastCompletedPlayId;

    public PlayLifecycleSystem(MatchState match, PlayState play, KickoffAfterScoreSystem? kickoffAfterScore = null)
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _kickoffAfterScore = kickoffAfterScore;
    }

    public void Update(World world)
    {
        _ = world;

        // Play selection starts a new pre-snap.
        foreach (var _ in SimEventBus.Drain<PlaySelectedEvent>())
            StartNewPreSnap();

        // Snap transitions to in-play.
        foreach (var e in SimEventBus.Drain<SnapEvent>())
        {
            _match.PossessionTeam = e.OffenseTeam;
            _play.Phase = PlayPhase.InPlay;
        }

        // Play end -> post-play.
        foreach (var e in SimEventBus.Drain<PlayEndedEvent>())
        {
            if (e.PlayId <= 0 || e.PlayId == _lastCompletedPlayId)
                continue;

            _kickoffAfterScore?.OnPlayEnded(e);

            _lastCompletedPlayId = e.PlayId;
            _play.Phase = PlayPhase.PostPlay;
            _play.PlayId = e.PlayId;
            _play.EndAbsoluteYard = e.EndAbsoluteYard;
            _play.Result = new PlayResult(e.YardsGained, e.Turnover, e.Touchdown, e.Safety);
            _play.WhistleReason = (WhistleReason)e.Reason;
        }

        // User acknowledges post-play / halftime break.
        foreach (var _ in SimEventBus.Drain<PostPlayContinueRequestedEvent>())
        {
            if (_match.Phase == MatchPhase.Halftime)
            {
                _play.Phase = PlayPhase.PreSnap;
                continue;
            }

            if (_match.Phase == MatchPhase.Final)
            {
                _play.Phase = PlayPhase.PostPlay;
                continue;
            }

            StartNewPreSnap();
        }
    }

    private void StartNewPreSnap()
    {
        _match.PlayNumber++;

        _play.ResetForNewPlay(
            playId: _match.PlayNumber,
            startAbsoluteYard: PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection));

        _play.Phase = PlayPhase.PreSnap;
    }
}
