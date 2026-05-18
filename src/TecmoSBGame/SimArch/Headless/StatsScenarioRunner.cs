using System.Collections.Generic;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Replay;
using TecmoSBGame.SimArch.State;
using TecmoSBGame.SimArch.Systems;

namespace TecmoSBGame.SimArch.Headless;

internal static class StatsScenarioRunner
{
    public static StatsScenarioSummary RunPassingScenario()
    {
        using var world = World.Create();
        var match = CreateMatch();
        var play = CreatePlay(match);
        var stats = new StatsState();
        var replay = new ReplayRecorder();
        var statsSystem = new StatsSystem();
        statsSystem.ResetForNewPlay(stats, play);
        replay.ResetForPlay(play);

        var qbId = CreatePlayer(world, new Vector2(40, 112), 0, true);
        var wrId = CreatePlayer(world, new Vector2(52, 112), 0, true);
        var ballId = CreateBall(world, wrId, new Vector2(52, 112));

        stats.CurrentPlay.PasserId = qbId;
        stats.CurrentPlay.ReceiverId = wrId;
        stats.CurrentPlay.PassingYards = 12;
        play.EndAbsoluteYard = play.StartAbsoluteYard + 12;
        play.Result = new PlayResult(12, false, false, false);
        play.Phase = PlayPhase.PostPlay;

        statsSystem.ApplyPlayEnd(world, ballId, match, play, stats);
        replay.Capture.Meta.FinalAbsoluteYard = play.EndAbsoluteYard;
        foreach (var evt in stats.EventLog)
            replay.RecordEvent(0, evt);

        return new StatsScenarioSummary(stats, replay.Capture);
    }

    public static StatsScenarioSummary RunRushingScenario()
    {
        using var world = World.Create();
        var match = CreateMatch();
        var play = CreatePlay(match);
        var stats = new StatsState();
        var replay = new ReplayRecorder();
        var statsSystem = new StatsSystem();
        statsSystem.ResetForNewPlay(stats, play);
        replay.ResetForPlay(play);

        var rbId = CreatePlayer(world, new Vector2(40, 112), 0, true);
        var ballId = CreateBall(world, rbId, new Vector2(46, 112));

        stats.CurrentPlay.BallCarrierId = rbId;
        play.EndAbsoluteYard = play.StartAbsoluteYard + 6;
        play.Result = new PlayResult(6, false, false, false);
        play.Phase = PlayPhase.PostPlay;

        statsSystem.ApplyPlayEnd(world, ballId, match, play, stats);
        replay.Capture.Meta.FinalAbsoluteYard = play.EndAbsoluteYard;
        foreach (var evt in stats.EventLog)
            replay.RecordEvent(0, evt);

        return new StatsScenarioSummary(stats, replay.Capture);
    }

    public static StatsScenarioSummary RunTurnoverScenario()
    {
        using var world = World.Create();
        var match = CreateMatch();
        var play = CreatePlay(match);
        var stats = new StatsState();
        var replay = new ReplayRecorder();
        var statsSystem = new StatsSystem();
        statsSystem.ResetForNewPlay(stats, play);
        replay.ResetForPlay(play);

        var qbId = CreatePlayer(world, new Vector2(40, 112), 0, true);
        var dbId = CreatePlayer(world, new Vector2(48, 112), 1, false);
        var ballId = CreateBall(world, dbId, new Vector2(48, 112));

        stats.CurrentPlay.PasserId = qbId;
        stats.CurrentPlay.InterceptorId = dbId;
        play.EndAbsoluteYard = play.StartAbsoluteYard + 8;
        play.Result = new PlayResult(8, true, false, false);
        play.Phase = PlayPhase.PostPlay;

        statsSystem.ApplyPlayEnd(world, ballId, match, play, stats);
        replay.Capture.Meta.FinalAbsoluteYard = play.EndAbsoluteYard;
        foreach (var evt in stats.EventLog)
            replay.RecordEvent(0, evt);

        return new StatsScenarioSummary(stats, replay.Capture);
    }

    private static MatchState CreateMatch() => new()
    {
        Quarter = 1,
        GameClockSeconds = 300,
        PossessionTeam = 0,
        OffenseDirection = OffenseDirection.LeftToRight,
        Down = 1,
        YardsToGo = 10,
        BallSpot = BallSpot.Own(40),
        PlayNumber = 1,
        DriveId = 0,
    };

    private static PlayState CreatePlay(MatchState match)
    {
        var play = new PlayState();
        play.ResetForNewPlay(1, PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection));
        return play;
    }

    private static int CreatePlayer(World world, Vector2 pos, int teamIndex, bool isOffense)
    {
        var e = world.Create(new Position { Value = pos }, new Team { TeamIndex = teamIndex, IsOffense = isOffense });
        return e.Id;
    }

    private static int CreateBall(World world, int ownerId, Vector2 pos)
    {
        var e = world.Create(new Ball { State = BallState.Held, OwnerEntityId = ownerId, IsComplete = true }, new Position { Value = pos });
        return e.Id;
    }
}

internal readonly record struct StatsScenarioSummary(StatsState Stats, ReplayCapture Replay);
