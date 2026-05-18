using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

public sealed class StatsSystem
{
    public void ResetForNewPlay(StatsState stats, PlayState play)
        => stats.ResetForNewPlay(play.PlayId);

    public void ApplyPlayEnd(World world, int ballEntityId, MatchState match, PlayState play, StatsState stats)
    {
        var playStats = stats.CurrentPlay;
        playStats.Turnover = play.Result.Turnover;

        var offenseTeam = match.PossessionTeam;
        var defenseTeam = 1 - offenseTeam;
        var offenseTeamStats = stats.Match.GetTeam(offenseTeam);
        var defenseTeamStats = stats.Match.GetTeam(defenseTeam);

        if (playStats.PasserId is int passerId)
        {
            offenseTeamStats.PassAttempts++;
            var passerStats = stats.Match.GetPlayer(passerId);
            passerStats.PassAttempts++;

            if (playStats.ReceiverId is int receiverId)
            {
                offenseTeamStats.PassCompletions++;
                offenseTeamStats.PassingYards += playStats.PassingYards;

                passerStats.PassCompletions++;
                passerStats.PassingYards += playStats.PassingYards;

                var receiverStats = stats.Match.GetPlayer(receiverId);
                receiverStats.Receptions++;
                receiverStats.ReceivingYards += playStats.PassingYards;

                stats.Record(new StatEventRecord(play.PlayId, "pass_complete", offenseTeam, passerId, playStats.PassingYards, false, $"receiver={receiverId}"));
                stats.Record(new StatEventRecord(play.PlayId, "reception", offenseTeam, receiverId, playStats.PassingYards, false, $"passer={passerId}"));
            }

            if (playStats.InterceptorId is int interceptorId)
            {
                offenseTeamStats.TurnoversCommitted++;
                defenseTeamStats.TurnoversForced++;
                defenseTeamStats.Interceptions++;

                passerStats.InterceptionsThrown++;
                passerStats.TurnoversCommitted++;

                var interceptorStats = stats.Match.GetPlayer(interceptorId);
                interceptorStats.InterceptionsCaught++;
                interceptorStats.TurnoversForced++;

                stats.Record(new StatEventRecord(play.PlayId, "interception", defenseTeam, interceptorId, 0, true, $"passer={passerId}"));
            }

            return;
        }

        if (playStats.BallCarrierId is int carrierId)
        {
            offenseTeamStats.RushingAttempts++;
            offenseTeamStats.RushingYards += play.Result.YardsGained;

            var carrierStats = stats.Match.GetPlayer(carrierId);
            carrierStats.RushingAttempts++;
            carrierStats.RushingYards += play.Result.YardsGained;

            stats.Record(new StatEventRecord(play.PlayId, "rush", offenseTeam, carrierId, play.Result.YardsGained, play.Result.Turnover, string.Empty));

            if (play.Result.Turnover)
            {
                offenseTeamStats.TurnoversCommitted++;
                defenseTeamStats.TurnoversForced++;
                carrierStats.TurnoversCommitted++;

                if (TryGetBallOwner(world, ballEntityId, out var recoveryId, out var recoveryTeam) && recoveryTeam == defenseTeam)
                {
                    defenseTeamStats.FumbleRecoveries++;
                    var recoveryStats = stats.Match.GetPlayer(recoveryId);
                    recoveryStats.FumblesRecovered++;
                    recoveryStats.TurnoversForced++;
                    stats.Record(new StatEventRecord(play.PlayId, "fumble_recovery", defenseTeam, recoveryId, 0, true, $"forced-by-play"));
                }
            }
        }
    }

    private static bool TryGetBallOwner(World world, int ballEntityId, out int ownerId, out int teamIndex)
    {
        ownerId = -1;
        teamIndex = -1;
        var localOwnerId = -1;
        var foundBall = false;
        var qBall = new QueryDescription().WithAll<Ball>();
        world.Query(in qBall, (Entity e, ref Ball ball) =>
        {
            if (foundBall || e.Id != ballEntityId)
                return;

            localOwnerId = ball.OwnerEntityId;
            foundBall = true;
        });

        if (!foundBall || localOwnerId <= 0)
            return false;

        var localTeamIndex = -1;
        var foundTeam = false;
        var qTeam = new QueryDescription().WithAll<Team>();
        world.Query(in qTeam, (Entity e, ref Team team) =>
        {
            if (foundTeam || e.Id != localOwnerId)
                return;

            localTeamIndex = team.TeamIndex;
            foundTeam = true;
        });

        if (!foundTeam)
            return false;

        ownerId = localOwnerId;
        teamIndex = localTeamIndex;
        return true;
    }
}
