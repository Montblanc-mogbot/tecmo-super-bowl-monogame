using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Deterministic transition hook: when a scoring play ends (TD/safety), set up the next kickoff.
///
/// Consumes <see cref="PlayEndedEvent"/> via Read() so other systems may also observe it.
/// </summary>
public sealed class KickoffAfterScoreSystem : UpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly bool _log;

    private int _lastProcessedPlayId = -1;

    public KickoffAfterScoreSystem(GameEvents? events, MatchState matchState, PlayState playState, bool log = true)
    {
        _events = events;
        _match = matchState;
        _play = playState;
        _log = log;
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        var ended = _events.Read<PlayEndedEvent>();
        if (ended.Count <= 0)
            return;

        // If multiple end events somehow arrive in a tick, process the latest playId deterministically.
        // (Normally there should be exactly one.)
        var e = ended[^1];
        if (e.PlayId == _lastProcessedPlayId)
            return;

        if (!e.Touchdown && !e.Safety)
        {
            _lastProcessedPlayId = e.PlayId;
            return;
        }

        // IMPORTANT: DownDistanceSystem runs earlier and must not mutate possession for scoring plays.
        // At this point, MatchState.PossessionTeam should still be the offense team for the scoring play.
        var offenseTeam = _match.PossessionTeam;

        int kickingTeam;
        int receivingTeam;
        KickoffSetupReason reason;

        if (e.Touchdown)
        {
            // After a TD, the scoring team kicks to the opponent.
            kickingTeam = offenseTeam;
            receivingTeam = 1 - offenseTeam;
            reason = KickoffSetupReason.AfterTouchdown;
        }
        else
        {
            // After a safety, the team that surrendered the safety kicks to the scoring team.
            // (NFL rule: free kick by the team that was scored upon.)
            kickingTeam = offenseTeam;
            receivingTeam = 1 - offenseTeam;
            reason = KickoffSetupReason.AfterSafety;
        }

        // Treat the kickoff as a new drive for now.
        _match.DriveId++;

        // Set up match-level kickoff view.
        _match.ResetForKickoff(kickingTeam, receivingTeam);

        // Reset play-level state for the next kickoff play.
        var startAbs = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);
        _play.ResetForNewPlay(_match.PlayNumber + 1, startAbs);

        _events.Publish(new KickoffSetupEvent(kickingTeam, receivingTeam, reason));

        if (_log)
            Console.WriteLine($"[kickoff] after score: reason={reason} kicking=T{kickingTeam} receiving=T{receivingTeam}");

        _lastProcessedPlayId = e.PlayId;
    }
}
