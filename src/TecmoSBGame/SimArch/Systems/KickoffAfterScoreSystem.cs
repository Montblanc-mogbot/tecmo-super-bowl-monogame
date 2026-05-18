using System;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Deterministic score -> kickoff transition hook for SimArch.
///
/// Consumes <see cref="PlayEndedEvent"/> without stealing it from <see cref="PlayLifecycleSystem"/>,
/// updates match/play state for the upcoming kickoff, and publishes <see cref="KickoffSetupEvent"/>.
/// </summary>
public sealed class KickoffAfterScoreSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly bool _log;

    private int _lastProcessedPlayId = -1;

    public KickoffAfterScoreSystem(MatchState match, PlayState play, bool log = false)
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _log = log;
    }

    public void OnPlayEnded(in PlayEndedEvent ended)
    {
        if (ended.PlayId <= 0 || ended.PlayId == _lastProcessedPlayId)
            return;

        _lastProcessedPlayId = ended.PlayId;

        if (!ended.Touchdown && !ended.Safety)
            return;

        var offenseTeam = _match.PossessionTeam;

        int kickingTeam;
        int receivingTeam;
        KickoffSetupReason reason;

        if (ended.Touchdown)
        {
            kickingTeam = offenseTeam;
            receivingTeam = 1 - offenseTeam;
            reason = KickoffSetupReason.AfterTouchdown;
        }
        else
        {
            kickingTeam = offenseTeam;
            receivingTeam = 1 - offenseTeam;
            reason = KickoffSetupReason.AfterSafety;
        }

        _match.ResetForKickoff(kickingTeam, receivingTeam, reason);
        _play.ResetForNewPlay(_match.PlayNumber + 1, PlayState.ToAbsoluteYard(BallSpot.Own(MatchState.TouchbackSpotYard), _match.OffenseDirection));

        var kickoff = new KickoffSetupEvent(kickingTeam, receivingTeam, reason);
        SimEventBus.Send(ref kickoff);

        if (_log)
            Console.WriteLine($"[sim-kickoff] after score: reason={reason} kicking=T{kickingTeam} receiving=T{receivingTeam} nextPlay={_match.PlayNumber + 1}");
    }
}
