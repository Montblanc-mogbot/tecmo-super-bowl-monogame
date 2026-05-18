using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.Factories;
using TecmoSBGame.SimArch.Headless;
using TecmoSBGame.SimArch.State;
using TecmoSBGame.SimArch.Systems;

namespace TecmoSBGame.SimArch;

public static class SimArchHeadless
{
    private const float DtSeconds = 1f / 60f;
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new() { WriteIndented = true };

    public static int RunTwoPlaysScenario(int ticks = 240)
    {
        var formationData = TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        var defensiveFormationData = TecmoSB.DefensiveFormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "defensive_formation_data.yaml"));
        var playList = TecmoSB.PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = TecmoSB.DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));
        var playData = TecmoSB.PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));

        using var sim = new Sim(formationData, defensiveFormationData, playList, playData, defensePlays);

        sim.ApplyPlaySelection(new Sim.PendingPlaySelection(
            PlayNumber: 10,
            FormationId: "00",
            OffensivePlayName: "DEMO",
            OffensivePlaySlot: "DEMO"));

        for (var i = 0; i < ticks; i++)
            sim.Update(dtSeconds: DtSeconds);

        if (sim.Snapshot.Tick <= 0)
        {
            Console.Error.WriteLine("[headless-2plays] FAIL: no ticks advanced");
            return 2;
        }

        Console.WriteLine($"[headless-2plays] OK ticks={sim.Snapshot.Tick} players={sim.Snapshot.Players.Length}");
        return 0;
    }

    public static int RunScrimmageScenarioPack()
    {
        var validations = new (string Name, Func<int> Run)[]
        {
            ("run-drive", RunDriveLifecycleScenario),
            ("pass-outcomes", RunPassOutcomesScenario),
            ("turnover-fumble", RunFumbleRecoveryScenario),
            ("scoring-scoreboard", RunScoreboardIntegrationScenario),
            ("reset-quarter-flow", RunQuarterFlowScenario),
        };

        foreach (var validation in validations)
        {
            var result = validation.Run();
            if (result != 0)
            {
                Console.Error.WriteLine($"[headless-scrimmage-pack] FAIL scenario={validation.Name} exit={result}");
                return result;
            }
        }

        Console.WriteLine("[headless-scrimmage-pack] PASS scenarios=run-drive,pass-outcomes,turnover-fumble,scoring-scoreboard,reset-quarter-flow");
        return 0;
    }

    public static int RunDeterminismCheck(string scenarioName, int runs = 2)
    {
        if (runs < 2)
        {
            Console.Error.WriteLine($"[headless-determinism] FAIL scenario={scenarioName} runs must be >= 2");
            return 2;
        }

        if (!TryNormalizeScenarioName(scenarioName, out var normalizedScenario))
        {
            Console.Error.WriteLine($"[headless-determinism] FAIL unknown scenario='{scenarioName}' supported=scrimmage-pack,pass-outcomes,drive,quarter-flow,scoreboard,fumble,stats,kickoff-after-score,kickoff,punt,field-goal,pressure");
            return 2;
        }

        var artifactRoot = Path.Combine("artifacts", "headless-determinism", $"{normalizedScenario}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}");
        Directory.CreateDirectory(artifactRoot);

        string? baselineJson = null;
        string? baselinePath = null;

        for (var runIndex = 1; runIndex <= runs; runIndex++)
        {
            if (!TryBuildDeterminismArtifact(normalizedScenario, out var artifactJson, out var error))
            {
                Console.Error.WriteLine($"[headless-determinism] FAIL scenario={normalizedScenario} run={runIndex} {error}");
                return 1;
            }

            var artifactPath = Path.Combine(artifactRoot, $"run{runIndex}.json");
            File.WriteAllText(artifactPath, artifactJson);

            if (baselineJson is null)
            {
                baselineJson = artifactJson;
                baselinePath = artifactPath;
                continue;
            }

            if (!string.Equals(baselineJson, artifactJson, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"[headless-determinism] FAIL scenario={normalizedScenario} run={runIndex} artifact mismatch");
                Console.Error.WriteLine($"  baseline={baselinePath}");
                Console.Error.WriteLine($"  actual={artifactPath}");
                Console.Error.WriteLine($"  diff={DescribeFirstDifference(baselineJson, artifactJson)}");
                return 1;
            }
        }

        Console.WriteLine($"[headless-determinism] PASS scenario={normalizedScenario} runs={runs} artifactDir={artifactRoot}");
        return 0;
    }

    public static int RunPassMetadataScenario()
        => RunPassOutcomesScenario();

    public static int RunPressureScenario(int ticks = 120)
    {
        var free = ExecutePressureCase(blockerCount: 0, ticks);
        var single = ExecutePressureCase(blockerCount: 1, ticks);
        var doubleTeam = ExecutePressureCase(blockerCount: 2, ticks);

        if (free is null || single is null || doubleTeam is null)
        {
            Console.Error.WriteLine("[headless-pressure] FAIL: scenario did not resolve");
            return 1;
        }

        if (free.Value.ActivePressureTicks <= single.Value.ActivePressureTicks)
        {
            Console.Error.WriteLine($"[headless-pressure] FAIL expected single blocker to reduce pressure got free={free.Value} single={single.Value}");
            return 1;
        }

        if (doubleTeam.Value.HelperAssignments < 2)
        {
            Console.Error.WriteLine($"[headless-pressure] FAIL expected double-team case to keep two helpers on the rusher: {doubleTeam.Value}");
            return 1;
        }

        if (single.Value.ActivePressureTicks <= doubleTeam.Value.ActivePressureTicks)
        {
            Console.Error.WriteLine($"[headless-pressure] FAIL expected double team to reduce pressure more than single blocker got single={single.Value} double={doubleTeam.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-pressure] PASS free={free.Value} single={single.Value} double={doubleTeam.Value}");
        return 0;
    }

    public static int RunPassOutcomesScenario()
    {
        var scenarios = new[]
        {
            PassScenarioKind.Completion,
            PassScenarioKind.Incomplete,
            PassScenarioKind.Interception,
            PassScenarioKind.CoverageBreakup,
            PassScenarioKind.PressureThrow,
        };

        foreach (var scenario in scenarios)
        {
            var first = ExecutePassScenario(scenario);
            var second = ExecutePassScenario(scenario);

            if (first is null || second is null)
            {
                Console.Error.WriteLine($"[headless-pass-outcomes] FAIL scenario={scenario} did not resolve");
                return 1;
            }

            if (!first.Value.Equals(second.Value))
            {
                Console.Error.WriteLine($"[headless-pass-outcomes] FAIL scenario={scenario} was not deterministic");
                Console.Error.WriteLine($"  first={first.Value}");
                Console.Error.WriteLine($"  second={second.Value}");
                return 1;
            }

            if (!ValidateScenario(first.Value, scenario, out var error))
            {
                Console.Error.WriteLine($"[headless-pass-outcomes] FAIL scenario={scenario} {error}");
                Console.Error.WriteLine($"  summary={first.Value}");
                return 1;
            }

            Console.WriteLine($"[headless-pass-outcomes] PASS scenario={scenario} summary={first.Value}");
        }

        return 0;
    }

    public static int RunFumbleRecoveryScenario()
    {
        var offenseRecovery = ExecuteFumbleScenario(defenseRecovers: false);
        var defenseRecovery = ExecuteFumbleScenario(defenseRecovers: true);

        if (offenseRecovery is null || defenseRecovery is null)
        {
            Console.Error.WriteLine("[headless-fumble] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidateFumbleScenario(offenseRecovery.Value, defenseRecovers: false, out var offenseError))
        {
            Console.Error.WriteLine($"[headless-fumble] FAIL offense-recovery {offenseError}");
            Console.Error.WriteLine($"  summary={offenseRecovery.Value}");
            return 1;
        }

        if (!ValidateFumbleScenario(defenseRecovery.Value, defenseRecovers: true, out var defenseError))
        {
            Console.Error.WriteLine($"[headless-fumble] FAIL defense-recovery {defenseError}");
            Console.Error.WriteLine($"  summary={defenseRecovery.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-fumble] PASS offense={offenseRecovery.Value} defense={defenseRecovery.Value}");
        return 0;
    }

    public static int RunDriveLifecycleScenario()
    {
        var summary = ExecuteDriveLifecycleScenario();
        if (summary is null)
        {
            Console.Error.WriteLine("[headless-drive] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidateDriveLifecycleScenario(summary.Value, out var error))
        {
            Console.Error.WriteLine($"[headless-drive] FAIL {error}");
            Console.Error.WriteLine($"  summary={summary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-drive] PASS summary={summary.Value}");
        return 0;
    }

    public static int RunQuarterFlowScenario()
    {
        var summary = ExecuteQuarterFlowScenario();
        if (summary is null)
        {
            Console.Error.WriteLine("[headless-quarter-flow] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidateQuarterFlowScenario(summary.Value, out var error))
        {
            Console.Error.WriteLine($"[headless-quarter-flow] FAIL {error}");
            Console.Error.WriteLine($"  summary={summary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-quarter-flow] PASS summary={summary.Value}");
        return 0;
    }

    public static int RunScoreboardIntegrationScenario()
    {
        var summary = ExecuteScoreboardIntegrationScenario();
        if (summary is null)
        {
            Console.Error.WriteLine("[headless-scoreboard] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidateScoreboardIntegrationScenario(summary.Value, out var error))
        {
            Console.Error.WriteLine($"[headless-scoreboard] FAIL {error}");
            Console.Error.WriteLine($"  summary={summary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-scoreboard] PASS summary={summary.Value}");
        return 0;
    }

    public static int RunStatsScenario()
    {
        var passing = StatsScenarioRunner.RunPassingScenario();
        if (!ValidatePassingStats(passing, out var passError))
        {
            Console.Error.WriteLine($"[headless-stats] FAIL passing {passError}");
            return 1;
        }

        var rushing = StatsScenarioRunner.RunRushingScenario();
        if (!ValidateRushingStats(rushing, out var rushError))
        {
            Console.Error.WriteLine($"[headless-stats] FAIL rushing {rushError}");
            return 1;
        }

        var turnover = StatsScenarioRunner.RunTurnoverScenario();
        if (!ValidateTurnoverStats(turnover, out var turnoverError))
        {
            Console.Error.WriteLine($"[headless-stats] FAIL turnover {turnoverError}");
            return 1;
        }

        Console.WriteLine("[headless-stats] PASS passing/rushing/turnover stat paths");
        return 0;
    }

    public static int RunKickoffAfterScoreScenario()
    {
        var summary = ExecuteKickoffAfterScoreScenario();
        if (summary is null)
        {
            Console.Error.WriteLine("[headless-kickoff-after-score] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidateKickoffAfterScoreScenario(summary.Value, out var error))
        {
            Console.Error.WriteLine($"[headless-kickoff-after-score] FAIL {error}");
            Console.Error.WriteLine($"  summary={summary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-kickoff-after-score] PASS summary={summary.Value}");
        return 0;
    }

    public static int RunKickoffScenario()
    {
        var returnSummary = ExecuteKickoffScenario(touchback: false);
        var touchbackSummary = ExecuteKickoffScenario(touchback: true);

        if (returnSummary is null || touchbackSummary is null)
        {
            Console.Error.WriteLine($"[headless-kickoff] FAIL scenario did not resolve returnNull={returnSummary is null} touchbackNull={touchbackSummary is null}");
            return 1;
        }

        if (!ValidateKickoffScenario(returnSummary.Value, touchback: false, out var returnError))
        {
            Console.Error.WriteLine($"[headless-kickoff] FAIL return {returnError}");
            Console.Error.WriteLine($"  summary={returnSummary.Value}");
            return 1;
        }

        if (!ValidateKickoffScenario(touchbackSummary.Value, touchback: true, out var touchbackError))
        {
            Console.Error.WriteLine($"[headless-kickoff] FAIL touchback {touchbackError}");
            Console.Error.WriteLine($"  summary={touchbackSummary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-kickoff] PASS return={returnSummary.Value} touchback={touchbackSummary.Value}");
        return 0;
    }

    public static int RunPuntScenario()
    {
        var returnSummary = ExecutePuntScenario(PuntScenarioKind.StandardReturn);
        var touchbackSummary = ExecutePuntScenario(PuntScenarioKind.Touchback);
        var downedSummary = ExecutePuntScenario(PuntScenarioKind.Downed);
        var muffSummary = ExecutePuntScenario(PuntScenarioKind.Muff);

        if (returnSummary is null || touchbackSummary is null || downedSummary is null || muffSummary is null)
        {
            Console.Error.WriteLine("[headless-punt] FAIL scenario did not resolve");
            return 1;
        }

        if (!ValidatePuntScenario(returnSummary.Value, PuntScenarioKind.StandardReturn, out var returnError))
        {
            Console.Error.WriteLine($"[headless-punt] FAIL return {returnError}");
            Console.Error.WriteLine($"  summary={returnSummary.Value}");
            return 1;
        }

        if (!ValidatePuntScenario(touchbackSummary.Value, PuntScenarioKind.Touchback, out var touchbackError))
        {
            Console.Error.WriteLine($"[headless-punt] FAIL touchback {touchbackError}");
            Console.Error.WriteLine($"  summary={touchbackSummary.Value}");
            return 1;
        }

        if (!ValidatePuntScenario(downedSummary.Value, PuntScenarioKind.Downed, out var downedError))
        {
            Console.Error.WriteLine($"[headless-punt] FAIL downed {downedError}");
            Console.Error.WriteLine($"  summary={downedSummary.Value}");
            return 1;
        }

        if (!ValidatePuntScenario(muffSummary.Value, PuntScenarioKind.Muff, out var muffError))
        {
            Console.Error.WriteLine($"[headless-punt] FAIL muff {muffError}");
            Console.Error.WriteLine($"  summary={muffSummary.Value}");
            return 1;
        }

        Console.WriteLine($"[headless-punt] PASS return={returnSummary.Value} touchback={touchbackSummary.Value} downed={downedSummary.Value} muff={muffSummary.Value}");
        return 0;
    }

    public static int RunFieldGoalScenario()
    {
        var goodFg = ExecuteFieldGoalScenario(FieldGoalScenarioKind.GoodFieldGoal);
        var missFg = ExecuteFieldGoalScenario(FieldGoalScenarioKind.MissedFieldGoal);
        var blockedPat = ExecuteFieldGoalScenario(FieldGoalScenarioKind.BlockedExtraPoint);
        var goodPat = ExecuteFieldGoalScenario(FieldGoalScenarioKind.GoodExtraPoint);

        if (goodFg is null || missFg is null || blockedPat is null || goodPat is null)
        {
            Console.Error.WriteLine("[headless-field-goal] FAIL scenario did not resolve");
            return 1;
        }

        string? goodError = null;
        string? missError = null;
        string? blockedError = null;
        string? patError = null;
        if (!ValidateFieldGoalScenario(goodFg.Value, FieldGoalScenarioKind.GoodFieldGoal, out goodError)
            || !ValidateFieldGoalScenario(missFg.Value, FieldGoalScenarioKind.MissedFieldGoal, out missError)
            || !ValidateFieldGoalScenario(blockedPat.Value, FieldGoalScenarioKind.BlockedExtraPoint, out blockedError)
            || !ValidateFieldGoalScenario(goodPat.Value, FieldGoalScenarioKind.GoodExtraPoint, out patError))
        {
            var message = goodError ?? missError ?? blockedError ?? patError;
            Console.Error.WriteLine($"[headless-field-goal] FAIL {message}");
            return 1;
        }

        Console.WriteLine($"[headless-field-goal] PASS fg={goodFg.Value} miss={missFg.Value} blockPat={blockedPat.Value} goodPat={goodPat.Value}");
        return 0;
    }

    private static FieldGoalScenarioSummary? ExecuteFieldGoalScenario(FieldGoalScenarioKind kind)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 4,
            YardsToGo = 8,
            BallSpot = kind is FieldGoalScenarioKind.GoodExtraPoint or FieldGoalScenarioKind.BlockedExtraPoint ? BallSpot.Opp(2) : BallSpot.Opp(18),
            PlayNumber = 9,
            DriveId = 2,
        };

        match.FieldGoalPending = true;
        match.ExtraPointPending = kind is FieldGoalScenarioKind.GoodExtraPoint or FieldGoalScenarioKind.BlockedExtraPoint;
        match.ForceFieldGoalMiss = kind == FieldGoalScenarioKind.MissedFieldGoal;
        match.ForceFieldGoalBlock = kind == FieldGoalScenarioKind.BlockedExtraPoint;
        match.FieldGoalTargetAbsoluteYardOverride = 100;
        if (match.ExtraPointPending)
            match.Team0Score = 6;

        var play = new PlayState();
        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(playId: match.PlayNumber, startAbsoluteYard: startAbs);
        play.Phase = PlayPhase.PreSnap;

        var lifecycle = new PlayLifecycleSystem(match, play, new KickoffAfterScoreSystem(match, play));
        var downDistance = new DownDistanceSystem();
        var fg = new FieldGoalPlaySystem();
        var ballSystem = new BallSystem();
        var control = new Control { ControlledEntityId = -1, PendingForcedEntityId = -1, PreviousControlledEntityId = -1 };
        var ballId = BallEntityFactory.CreateBall(world, new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs), 112f));

        var kickRoles = new[] { RoleId.QB, RoleId.HB, RoleId.LT, RoleId.LG, RoleId.OC, RoleId.RG, RoleId.RT, RoleId.TE, RoleId.WR1, RoleId.WR2, RoleId.FB };
        var kickKinds = new[] { PlayerRoleKind.K, PlayerRoleKind.QB, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.TE, PlayerRoleKind.WR, PlayerRoleKind.RB, PlayerRoleKind.RB };
        var defRoles = new[] { RoleId.DL1, RoleId.DL2, RoleId.DL3, RoleId.DL4, RoleId.LB1, RoleId.LB2, RoleId.LB3, RoleId.CB1, RoleId.CB2, RoleId.S1, RoleId.S2 };
        var defKinds = new[] { PlayerRoleKind.DL, PlayerRoleKind.DL, PlayerRoleKind.DL, PlayerRoleKind.DL, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB };

        for (var i = 0; i < 11; i++)
        {
            _ = CreatePlayer(world, new Vector2(40f + i, 60f + (i * 8f % 100f)), teamIndex: 0, isOffense: true, kickRoles[i], kickKinds[i], $"FG{i}", new Ratings { HP = 60, RS = 55, MS = 58 });
            _ = CreatePlayer(world, new Vector2(180f + i, 60f + (i * 8f % 100f)), teamIndex: 1, isOffense: false, defRoles[i], defKinds[i], $"D{i}", new Ratings { HP = 64, RS = 58, MS = 60 });
        }

        var uiSnap = new SnapEvent(match.PossessionTeam, 1 - match.PossessionTeam);
        SimEventBus.Send(ref uiSnap);
        lifecycle.Update(world);

        var offenseIds = new List<int>();
        var defenseIds = new List<int>();
        fg.UpdatePreMovement(world, match, play, ballId, offenseIds, defenseIds, ref control);
        for (var i = 0; i < 80 && !play.IsOver; i++)
        {
            ballSystem.Update(world, 1f / 60f);
            fg.UpdatePostBall(world, match, play, ballId, ref control);
            lifecycle.Update(world);
        }

        if (play.IsOver)
            downDistance.ApplyPlayEnd(match, play);

        return new FieldGoalScenarioSummary(kind, Snapshot(match, play), match.PendingKickoffReason, match.Team0Score, match.Team1Score);
    }

    private static bool ValidateFieldGoalScenario(FieldGoalScenarioSummary summary, FieldGoalScenarioKind kind, out string error)
    {
        error = string.Empty;
        switch (kind)
        {
            case FieldGoalScenarioKind.GoodFieldGoal:
                if (summary.Team0Score != 6 || summary.Team1Score != 0 || summary.PendingKickoffReason != KickoffSetupReason.AfterTouchdown || summary.Snapshot.WhistleReason != WhistleReason.Touchdown)
                    error = $"good FG should add 3 onto existing 3-point path and queue kickoff, got {summary}";
                break;
            case FieldGoalScenarioKind.MissedFieldGoal:
                if (summary.Team0Score != 0 || summary.Snapshot.PossessionTeam != 1 || summary.Snapshot.WhistleReason != WhistleReason.Turnover)
                    error = $"missed FG should flip possession with no points, got {summary}";
                break;
            case FieldGoalScenarioKind.BlockedExtraPoint:
                if (summary.Team0Score != 6 || summary.Snapshot.PossessionTeam != 1 || summary.Snapshot.WhistleReason != WhistleReason.Turnover || summary.Snapshot.BallSpot != BallSpot.Own(5))
                    error = $"blocked PAT should stay 6 and continue from the recovery spot, got {summary}";
                break;
            case FieldGoalScenarioKind.GoodExtraPoint:
                if (summary.Team0Score != 12 || summary.PendingKickoffReason != KickoffSetupReason.AfterTouchdown || summary.Snapshot.WhistleReason != WhistleReason.Touchdown)
                    error = $"good PAT should add 1 on top of the seeded touchdown state and queue kickoff, got {summary}";
                break;
        }

        return error.Length == 0;
    }

    private static PassScenarioSummary? ExecutePassScenario(PassScenarioKind scenario)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
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

        var play = new PlayState();
        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(playId: 1, startAbsoluteYard: startAbs);
        play.Phase = PlayPhase.InPlay;

        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();
        var playResult = new PlayResultResolver(match, play);
        var playEnd = new PlayEndSystem();
        var ballSystem = new BallSystem();
        var passComplete = new PassFlightCompleteSystem();
        var tackleResolution = new TackleResolutionSystem();
        var rushSystem = new DefensiveRushSystem();
        var coverageSystem = new CoverageSystem();
        var qbAiSystem = new QbAiSystem();
        var control = new Control { ControlledEntityId = -1, PendingForcedEntityId = -1, PreviousControlledEntityId = -1 };

        var qbPos = new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs), 112f);
        var receiverTargetPos = new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs + 7), 112f);
        var incompleteReceiverEndPos = new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs + 30), 60f);
        var defenderTacklePos = new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs + 12), 112f);

        var qbId = CreatePlayer(world, qbPos, teamIndex: 0, isOffense: true, RoleId.QB, PlayerRoleKind.QB, "QB", new Ratings { HP = 100, RS = 100, MS = 100 });
        var receiverId = CreatePlayer(world, receiverTargetPos, teamIndex: 0, isOffense: true, RoleId.WR1, PlayerRoleKind.WR, "WR1", new Ratings { HP = 5, RS = 5, MS = 5 });
        var receiver2Id = CreatePlayer(world, receiverTargetPos + new Vector2(0f, 22f), teamIndex: 0, isOffense: true, RoleId.WR2, PlayerRoleKind.WR, "WR2", new Ratings { HP = 25, RS = 25, MS = 25 });
        var pressureReceiverId = receiver2Id;
        var defenderId = CreatePlayer(world, defenderTacklePos, teamIndex: 1, isOffense: false, RoleId.CB1, PlayerRoleKind.DB, "CB1", new Ratings { HP = 100, RS = 100, MS = 100 });
        var passRushId = CreatePlayer(world, qbPos + new Vector2(8f, 10f), teamIndex: 1, isOffense: false, RoleId.DL1, PlayerRoleKind.DL, "DL1", new Ratings { HP = 100, RS = 100, MS = 100 });
        var ballId = BallEntityFactory.CreateBall(world, qbPos);
        SetBallHeld(world, ballId, qbId);

        SetQbBrain(world, qbId, QbBrain.Default with { DropbackFramesRemaining = 0, ReadTimeLimitFrames = 20, PressureThresholdFrames = 12 });
        SetCoverage(world, defenderId, new Coverage
        {
            Type = CoverageType.ManToMan,
            AssignmentTargetId = receiverId,
            Zone = ZoneLandmark.HookLeft,
            LandmarkPosition = Vector2.Zero,
            InPursuit = false,
            PursuitTargetId = -1,
            ReactionDelay = 1,
            ReactionTimer = 0,
            HasReacted = false,
            BreakDelayFrames = 0,
            BallHawkLeverage = 0f,
        });
        SetRush(world, passRushId, new Rush { Assignment = RushAssignment.AGapLeft, HasLandmark = true, Landmark = qbPos + new Vector2(8f, 10f), ReachedLandmark = true, Engaged = false });

        switch (scenario)
        {
            case PassScenarioKind.Completion:
                SetPosition(world, receiverId, receiverTargetPos);
                SetPosition(world, defenderId, defenderTacklePos);
                break;
            case PassScenarioKind.Incomplete:
                SetPosition(world, receiverId, receiverTargetPos);
                SetPosition(world, defenderId, defenderTacklePos + new Vector2(20f, 20f));
                break;
            case PassScenarioKind.Interception:
                SetPosition(world, receiverId, receiverTargetPos + new Vector2(28f, 10f));
                SetPosition(world, defenderId, receiverTargetPos + new Vector2(0f, 0f));
                SetCoverage(world, defenderId, GetCoverage(world, defenderId) with { BallHawkLeverage = 20f, BreakDelayFrames = 0 });
                break;
            case PassScenarioKind.CoverageBreakup:
                SetPosition(world, receiverId, receiverTargetPos + new Vector2(4f, 0f));
                SetPosition(world, defenderId, receiverTargetPos + new Vector2(7f, 0f));
                SetCoverage(world, defenderId, GetCoverage(world, defenderId) with { BallHawkLeverage = 2.5f, BreakDelayFrames = 0 });
                break;
            case PassScenarioKind.PressureThrow:
                SetPosition(world, receiverId, receiverTargetPos + new Vector2(12f, 0f));
                SetPosition(world, pressureReceiverId, qbPos + new Vector2(6f, 18f));
                SetPosition(world, defenderId, receiverTargetPos + new Vector2(3f, 0f));
                SetCoverage(world, defenderId, GetCoverage(world, defenderId) with { AssignmentTargetId = receiverId, BallHawkLeverage = 1.5f, BreakDelayFrames = 0 });
                SetQbBrain(world, qbId, QbBrain.Default with { DropbackFramesRemaining = 0, ReadTimeLimitFrames = 1, PressureThresholdFrames = 1 });
                break;
        }

        if (scenario is PassScenarioKind.Completion or PassScenarioKind.Incomplete or PassScenarioKind.CoverageBreakup)
        {
            PassFlightStartSystem.StartPass(world, ballId, qbId, receiverId, PassType.Bullet);
        }
        else if (scenario == PassScenarioKind.Interception)
        {
            PassFlightStartSystem.StartPass(world, ballId, qbId, receiverId, PassType.Bullet);
            SetPosition(world, defenderId, GetBall(world, ballId).EndPos);
        }
        else
        {
            for (var tick = 0; tick < 45; tick++)
            {
                rushSystem.Update(world, DtSeconds, new[] { defenderId, passRushId });
                coverageSystem.Update(world, DtSeconds, ballId, new[] { defenderId, passRushId });
                qbAiSystem.Update(world, DtSeconds, ballId);
                ballSystem.Update(world, DtSeconds);
                passComplete.Update(world);

                var requested = false;
                foreach (var passRequested in SimEventBus.Drain<PassRequestedEvent>())
                {
                    requested = true;
                    if (passRequested.TargetId != pressureReceiverId)
                    {
                        DrainResidualEvents();
                        return new PassScenarioSummary(
                            scenario,
                            PassOutcome.Incomplete,
                            passRequested.TargetId ?? -1,
                            BallState.Held,
                            GetBall(world, ballId).OwnerEntityId,
                            play.Phase,
                            play.WhistleReason,
                            false,
                            0,
                            play.EndAbsoluteYard,
                            match.PossessionTeam,
                            match.Down,
                            match.YardsToGo,
                            match.DriveId,
                            match.OffenseDirection,
                            passRequested.TargetId,
                            true);
                    }

                    SetPosition(world, defenderId, GetPosition(world, receiverId));
                }

                if (requested)
                    break;
            }
        }

        if (scenario == PassScenarioKind.Incomplete)
            SetPosition(world, receiverId, incompleteReceiverEndPos);

        for (var tick = 0; tick < 180; tick++)
        {
            if (scenario == PassScenarioKind.PressureThrow)
            {
                rushSystem.Update(world, DtSeconds, new[] { defenderId, passRushId });
                coverageSystem.Update(world, DtSeconds, ballId, new[] { defenderId, passRushId });
            }

            ballSystem.Update(world, DtSeconds);
            passComplete.Update(world);

            foreach (var resolved in SimEventBus.Drain<PassResolvedEvent>())
            {
                var liveOwnerId = GetBall(world, ballId).OwnerEntityId;

                if (resolved.Outcome == PassOutcome.Incomplete)
                {
                    var ended = new PlayEndedEvent(
                        PlayId: play.PlayId,
                        Reason: (int)WhistleReason.Incomplete,
                        EndAbsoluteYard: play.StartAbsoluteYard,
                        YardsGained: 0,
                        Turnover: false,
                        Touchdown: false,
                        Safety: false);
                    SimEventBus.Send(ref ended);
                    lifecycle.Update(world);
                    downDistance.ApplyPlayEnd(match, play);
                    playEnd.Update(world, ballId, match, play);
                    DrainResidualEvents();
                    return BuildSummary(scenario, resolved.Outcome, liveOwnerId, match, play, GetBall(world, ballId), resolved.TargetId, pressureForcedRead: scenario == PassScenarioKind.PressureThrow);
                }

                var carrierId = resolved.WinnerId ?? -1;
                if (carrierId <= 0)
                    return null;

                var carrierPos = GetPosition(world, carrierId);
                var tacklerId = resolved.Outcome == PassOutcome.Interception ? qbId : defenderId;
                var contact = new TackleContactEvent(tacklerId, carrierId, carrierPos);
                SimEventBus.Send(ref contact);
                tackleResolution.Update(world, DtSeconds, ballId, ref control);
                _ = SimEventBus.Drain<TackleResolvedEvent>();

                if (!tackleResolution.WhistledThisTick)
                    return null;

                playResult.ResolveOnTackle(world, ballId);
                var tackleEnded = new PlayEndedEvent(
                    PlayId: play.PlayId,
                    Reason: (int)WhistleReason.Tackle,
                    EndAbsoluteYard: play.EndAbsoluteYard,
                    YardsGained: play.Result.YardsGained,
                    Turnover: play.Result.Turnover,
                    Touchdown: play.Result.Touchdown,
                    Safety: play.Result.Safety);
                SimEventBus.Send(ref tackleEnded);
                lifecycle.Update(world);
                downDistance.ApplyPlayEnd(match, play);
                playEnd.Update(world, ballId, match, play);
                DrainResidualEvents();
                return BuildSummary(scenario, resolved.Outcome, liveOwnerId, match, play, GetBall(world, ballId), resolved.TargetId, pressureForcedRead: scenario == PassScenarioKind.PressureThrow);
            }
        }

        DrainResidualEvents();
        return null;
    }

    private static PassScenarioSummary BuildSummary(PassScenarioKind scenario, PassOutcome outcome, int liveOwnerId, MatchState match, PlayState play, Ball finalBall, int? targetedReceiverId, bool pressureForcedRead)
        => new(
            scenario,
            outcome,
            liveOwnerId,
            finalBall.State,
            finalBall.OwnerEntityId,
            play.Phase,
            play.WhistleReason,
            play.Result.Turnover,
            play.Result.YardsGained,
            play.EndAbsoluteYard,
            match.PossessionTeam,
            match.Down,
            match.YardsToGo,
            match.DriveId,
            match.OffenseDirection,
            targetedReceiverId,
            pressureForcedRead);

    private static bool ValidateScenario(PassScenarioSummary summary, PassScenarioKind scenario, out string error)
    {
        error = string.Empty;

        if (summary.Phase != PlayPhase.PostPlay)
        {
            error = $"expected PostPlay, got {summary.Phase}";
            return false;
        }

        if (summary.FinalBallState != BallState.Dead || summary.FinalBallOwnerId != -1)
        {
            error = $"expected dead post-play ball, got state={summary.FinalBallState} owner={summary.FinalBallOwnerId}";
            return false;
        }

        switch (scenario)
        {
            case PassScenarioKind.Completion:
                if (summary.Outcome != PassOutcome.Catch || summary.LiveOwnerId <= 0)
                {
                    error = "completion did not resolve to a live-ball catch";
                    return false;
                }
                if (summary.Turnover || summary.PossessionTeam != 0 || summary.Down != 2)
                {
                    error = $"completion should stay with team 0 on 2nd down; got turnover={summary.Turnover} poss={summary.PossessionTeam} down={summary.Down}";
                    return false;
                }
                break;

            case PassScenarioKind.Incomplete:
            case PassScenarioKind.CoverageBreakup:
                if (summary.Outcome != PassOutcome.Incomplete || summary.LiveOwnerId != -1)
                {
                    error = "incompletion did not dead-ball immediately";
                    return false;
                }
                if (summary.WhistleReason != WhistleReason.Incomplete || summary.Turnover || summary.PossessionTeam != 0 || summary.Down != 2)
                {
                    error = $"incompletion should whistle incomplete and keep team 0 on 2nd down; got whistle={summary.WhistleReason} turnover={summary.Turnover} poss={summary.PossessionTeam} down={summary.Down}";
                    return false;
                }
                break;

            case PassScenarioKind.Interception:
                if (summary.Outcome != PassOutcome.Interception || summary.LiveOwnerId <= 0)
                {
                    error = "interception did not produce a live-ball owner";
                    return false;
                }
                if (!summary.Turnover || summary.PossessionTeam != 1 || summary.Down != 1 || summary.DriveId != 1 || summary.OffenseDirection != OffenseDirection.RightToLeft)
                {
                    error = $"interception should flip possession/direction/reset downs; got turnover={summary.Turnover} poss={summary.PossessionTeam} down={summary.Down} drive={summary.DriveId} dir={summary.OffenseDirection}";
                    return false;
                }
                break;

            case PassScenarioKind.PressureThrow:
                if (!summary.PressureForcedRead || summary.TargetedReceiverId is null || summary.TargetedReceiverId <= 0)
                {
                    error = "pressure scenario did not capture forced alternate read";
                    return false;
                }
                break;
        }

        return true;
    }

    private static FumbleScenarioSummary? ExecuteFumbleScenario(bool defenseRecovers)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 2,
            YardsToGo = 6,
            BallSpot = BallSpot.Own(30),
            PlayNumber = 1,
            DriveId = 0,
        };

        var play = new PlayState();
        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(playId: 1, startAbsoluteYard: startAbs);
        play.Phase = PlayPhase.InPlay;

        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();
        var playResult = new PlayResultResolver(match, play);
        var playEnd = new PlayEndSystem();
        var ballSystem = new BallSystem();
        var tackleResolution = new TackleResolutionSystem();
        var fumbleResolution = new FumbleResolutionSystem();
        var looseBallPickup = new LooseBallPickupSystem { PickupRadius = 16f };
        var control = new Control { ControlledEntityId = -1, PendingForcedEntityId = -1, PreviousControlledEntityId = -1 };

        var carrierPos = new Vector2(FieldMapping.AbsoluteYardToWorldX(startAbs + 4), 112f);
        var tacklerPos = carrierPos + new Vector2(-3f, 0f);
        var offenseRecoveryPos = carrierPos + new Vector2(8f, 0f);
        var defenseRecoveryPos = carrierPos + new Vector2(8f, 0f);

        var carrierId = CreatePlayer(world, carrierPos, teamIndex: 0, isOffense: true, RoleId.HB, PlayerRoleKind.RB, "HB", new Ratings { HP = 10, RS = 5, MS = 20 });
        var offenseHelperId = CreatePlayer(world, offenseRecoveryPos, teamIndex: 0, isOffense: true, RoleId.WR1, PlayerRoleKind.WR, "WR1", new Ratings { HP = 40, RS = 40, MS = 40 });
        var tacklerId = CreatePlayer(world, tacklerPos, teamIndex: 1, isOffense: false, RoleId.DL1, PlayerRoleKind.DL, "DL1", new Ratings { HP = 100, RS = 100, MS = 100 });
        var defenderRecoveryId = CreatePlayer(world, defenseRecoveryPos, teamIndex: 1, isOffense: false, RoleId.LB1, PlayerRoleKind.LB, "LB1", new Ratings { HP = 60, RS = 60, MS = 60 });
        var ballId = BallEntityFactory.CreateBall(world, carrierPos);
        SetBallHeld(world, ballId, carrierId);

        SetPosition(world, carrierId, carrierPos + new Vector2(-18f, -18f));
        SetPosition(world, tacklerId, carrierPos + new Vector2(-22f, -22f));

        if (defenseRecovers)
        {
            SetPosition(world, offenseHelperId, carrierPos + new Vector2(24f, 16f));
            SetPosition(world, defenderRecoveryId, carrierPos + new Vector2(8f, 0f));
        }
        else
        {
            SetPosition(world, offenseHelperId, carrierPos + new Vector2(8f, 0f));
            SetPosition(world, defenderRecoveryId, carrierPos + new Vector2(24f, 16f));
        }

        var forced = false;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var contact = new TackleContactEvent(tacklerId, carrierId, carrierPos);
            SimEventBus.Send(ref contact);
            tackleResolution.Update(world, 0.30f, ballId, ref control, play);
            _ = SimEventBus.Drain<TackleResolvedEvent>();
            if (tackleResolution.FumbleTriggeredThisTick)
            {
                forced = true;
                break;
            }
        }

        if (!forced)
        {
            DrainResidualEvents();
            return null;
        }

        var sawFumble = false;
        foreach (var _ in SimEventBus.Drain<FumbleEvent>())
            sawFumble = true;

        var looseOwner = GetBall(world, ballId).OwnerEntityId;
        if (!sawFumble || looseOwner != -1)
        {
            DrainResidualEvents();
            return null;
        }

        for (var tick = 0; tick < 20; tick++)
        {
            ballSystem.Update(world, DtSeconds);
            fumbleResolution.Update(world, ballId);
            looseBallPickup.Update(world, ballId, ref control, match, play);
            if (!looseBallPickup.RecoveredThisTick)
                continue;

            playResult.ResolveOnTackle(world, ballId);
            var ended = new PlayEndedEvent(
                PlayId: play.PlayId,
                Reason: (int)(looseBallPickup.TurnoverThisTick ? WhistleReason.Turnover : WhistleReason.Tackle),
                EndAbsoluteYard: play.EndAbsoluteYard,
                YardsGained: play.Result.YardsGained,
                Turnover: play.Result.Turnover,
                Touchdown: play.Result.Touchdown,
                Safety: play.Result.Safety);
            SimEventBus.Send(ref ended);
            lifecycle.Update(world);
            downDistance.ApplyPlayEnd(match, play);
            playEnd.Update(world, ballId, match, play);

            var recoveredByDefense = looseBallPickup.RecoveringTeamThisTick == 1;
            var summary = new FumbleScenarioSummary(
                recoveredByDefense,
                looseBallPickup.RecoveringPlayerThisTick,
                match.PossessionTeam,
                match.Down,
                match.YardsToGo,
                match.DriveId,
                match.OffenseDirection,
                play.WhistleReason,
                play.Result.Turnover,
                play.Result.YardsGained,
                play.EndAbsoluteYard,
                GetBall(world, ballId).State,
                GetBall(world, ballId).OwnerEntityId,
                play.Phase);
            DrainResidualEvents();
            return summary;
        }

        DrainResidualEvents();
        return null;
    }

    private static bool ValidateFumbleScenario(FumbleScenarioSummary summary, bool defenseRecovers, out string error)
    {
        error = string.Empty;

        if (summary.Phase != PlayPhase.PostPlay)
        {
            error = $"expected PostPlay got {summary.Phase}";
            return false;
        }

        if (summary.FinalBallState != BallState.Dead || summary.FinalBallOwnerId != -1)
        {
            error = $"expected dead post-play ball got state={summary.FinalBallState} owner={summary.FinalBallOwnerId}";
            return false;
        }

        if (summary.RecoveredByDefense != defenseRecovers)
        {
            error = $"expected RecoveredByDefense={defenseRecovers} got {summary.RecoveredByDefense}";
            return false;
        }

        if (defenseRecovers)
        {
            if (!summary.Turnover || summary.PossessionTeam != 1 || summary.Down != 1 || summary.DriveId != 1 || summary.WhistleReason != WhistleReason.Turnover || summary.OffenseDirection != OffenseDirection.RightToLeft)
            {
                error = $"turnover recovery should flip possession/reset downs got turnover={summary.Turnover} poss={summary.PossessionTeam} down={summary.Down} drive={summary.DriveId} whistle={summary.WhistleReason} dir={summary.OffenseDirection}";
                return false;
            }
        }
        else
        {
            if (summary.Turnover || summary.PossessionTeam != 0 || summary.Down != 3 || summary.WhistleReason != WhistleReason.Tackle)
            {
                error = $"same-team recovery should keep possession and advance to 3rd down got turnover={summary.Turnover} poss={summary.PossessionTeam} down={summary.Down} whistle={summary.WhistleReason}";
                return false;
            }
        }

        return true;
    }

    private static DriveLifecycleScenarioSummary? ExecuteDriveLifecycleScenario()
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 1,
            YardsToGo = 10,
            BallSpot = BallSpot.Own(30),
            PlayNumber = 0,
            DriveId = 0,
        };

        var play = new PlayState();
        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();

        var selected = new PlaySelectedEvent("00", "DRIVE", "DRIVE", 10, string.Empty);
        SimEventBus.Send(ref selected);
        lifecycle.Update(world);
        var play1StartId = play.PlayId;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Tackle, endAbsoluteYard: 35, yardsGained: 5, turnover: false, touchdown: false, safety: false);
        var afterRun = Snapshot(match, play);

        var continue1 = new PostPlayContinueRequestedEvent();
        SimEventBus.Send(ref continue1);
        lifecycle.Update(world);
        var play2StartId = play.PlayId;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Tackle, endAbsoluteYard: 45, yardsGained: 10, turnover: false, touchdown: false, safety: false);
        var afterFirstDown = Snapshot(match, play);

        var continue2 = new PostPlayContinueRequestedEvent();
        SimEventBus.Send(ref continue2);
        lifecycle.Update(world);
        var play3StartId = play.PlayId;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Touchdown, endAbsoluteYard: 100, yardsGained: 55, turnover: false, touchdown: true, safety: false);
        var afterTouchdown = Snapshot(match, play);

        var continue3 = new PostPlayContinueRequestedEvent();
        SimEventBus.Send(ref continue3);
        lifecycle.Update(world);
        var play4StartId = play.PlayId;

        match.PossessionTeam = 1;
        match.OffenseDirection = OffenseDirection.RightToLeft;
        match.Down = 1;
        match.YardsToGo = 10;
        match.BallSpot = BallSpot.Own(25);
        match.DriveId = 1;
        play.ResetForNewPlay(play4StartId, PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection));
        play.Phase = PlayPhase.InPlay;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Turnover, endAbsoluteYard: 59, yardsGained: -16, turnover: true, touchdown: false, safety: false);
        var afterTurnover = Snapshot(match, play);

        return new DriveLifecycleScenarioSummary(
            play1StartId,
            play2StartId,
            play3StartId,
            play4StartId,
            afterRun,
            afterFirstDown,
            afterTouchdown,
            afterTurnover);
    }

    private static void CompleteDrivePlay(World world, PlayLifecycleSystem lifecycle, DownDistanceSystem downDistance, MatchState match, PlayState play, WhistleReason reason, int endAbsoluteYard, int yardsGained, bool turnover, bool touchdown, bool safety)
    {
        var ended = new PlayEndedEvent(
            PlayId: play.PlayId,
            Reason: (int)reason,
            EndAbsoluteYard: endAbsoluteYard,
            YardsGained: yardsGained,
            Turnover: turnover,
            Touchdown: touchdown,
            Safety: safety);
        SimEventBus.Send(ref ended);
        lifecycle.Update(world);
        downDistance.ApplyPlayEnd(match, play);
    }

    private static DriveCheckpoint Snapshot(MatchState match, PlayState play)
        => new(match.PossessionTeam, match.Down, match.YardsToGo, match.BallSpot, match.Team0Score, match.Team1Score, match.DriveId, match.PlayNumber, match.OffenseDirection, play.Phase, play.WhistleReason, play.EndAbsoluteYard, match.GoalToGo);

    private static bool ValidateDriveLifecycleScenario(DriveLifecycleScenarioSummary summary, out string error)
    {
        error = string.Empty;

        if (summary.Play1StartId <= 0 || summary.Play2StartId != summary.Play1StartId + 1 || summary.Play3StartId != summary.Play2StartId + 1 || summary.Play4StartId != summary.Play3StartId + 1)
        {
            error = $"expected monotonic +1 play id progression got {summary.Play1StartId},{summary.Play2StartId},{summary.Play3StartId},{summary.Play4StartId}";
            return false;
        }

        if (summary.AfterRun.PossessionTeam != 0 || summary.AfterRun.Down != 2 || summary.AfterRun.YardsToGo != 5 || summary.AfterRun.GoalToGo || !summary.AfterRun.BallSpot.Equals(BallSpot.Own(35)) || summary.AfterRun.PlayNumber != summary.Play1StartId)
        {
            error = $"run checkpoint wrong {summary.AfterRun}";
            return false;
        }

        if (summary.AfterFirstDown.PossessionTeam != 0 || summary.AfterFirstDown.Down != 1 || summary.AfterFirstDown.YardsToGo != 10 || summary.AfterFirstDown.GoalToGo || !summary.AfterFirstDown.BallSpot.Equals(BallSpot.Own(45)) || summary.AfterFirstDown.PlayNumber != summary.Play2StartId)
        {
            error = $"first-down checkpoint wrong {summary.AfterFirstDown}";
            return false;
        }

        if (summary.AfterTouchdown.Team0Score != 6 || summary.AfterTouchdown.Team1Score != 0 || summary.AfterTouchdown.PossessionTeam != 0 || summary.AfterTouchdown.Down != 1 || summary.AfterTouchdown.YardsToGo != 10 || summary.AfterTouchdown.GoalToGo || !summary.AfterTouchdown.BallSpot.Equals(BallSpot.Own(25)) || summary.AfterTouchdown.DriveId != 0 || summary.AfterTouchdown.WhistleReason != WhistleReason.Touchdown || summary.AfterTouchdown.PlayNumber != summary.Play3StartId)
        {
            error = $"touchdown checkpoint wrong {summary.AfterTouchdown}";
            return false;
        }

        if (summary.AfterTurnover.Team0Score != 6 || summary.AfterTurnover.PossessionTeam != 0 || summary.AfterTurnover.Down != 1 || summary.AfterTurnover.YardsToGo != 10 || summary.AfterTurnover.GoalToGo || summary.AfterTurnover.DriveId != 2 || summary.AfterTurnover.OffenseDirection != OffenseDirection.LeftToRight || !summary.AfterTurnover.BallSpot.Equals(BallSpot.Opp(41)) || summary.AfterTurnover.WhistleReason != WhistleReason.Turnover || summary.AfterTurnover.PlayNumber != summary.Play4StartId)
        {
            error = $"turnover checkpoint wrong {summary.AfterTurnover}";
            return false;
        }

        return true;
    }

    private static QuarterFlowScenarioSummary? ExecuteQuarterFlowScenario()
    {
        DrainResidualEvents();

        var clock = new GameClockSystem();
        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 1,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 2,
            YardsToGo = 7,
            BallSpot = BallSpot.Own(33),
            PlayNumber = 9,
            DriveId = 0,
            Phase = MatchPhase.FirstQuarter,
            KickingTeamIndex = 1,
            ReceivingTeamIndex = 0,
            DeferredKickKickingTeam = 1,
            DeferredKickReceivingTeam = 0,
        };

        var play = new PlayState();
        play.ResetForNewPlay(playId: 10, startAbsoluteYard: PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection));
        play.Phase = PlayPhase.InPlay;

        for (var i = 0; i < 60; i++)
            clock.Update(match, play);
        var afterQ1 = new QuarterCheckpoint(match.Quarter, match.GameClockSeconds, match.Phase, match.OffenseDirection, match.PossessionTeam, match.BallSpot, match.MatchOver);

        match.GameClockSeconds = 1;
        match.Phase = MatchPhase.SecondQuarter;
        play.Phase = PlayPhase.InPlay;
        for (var i = 0; i < 60; i++)
            clock.Update(match, play);
        var halftime = new QuarterCheckpoint(match.Quarter, match.GameClockSeconds, match.Phase, match.OffenseDirection, match.PossessionTeam, match.BallSpot, match.MatchOver);

        play.Phase = PlayPhase.PreSnap;
        clock.AdvanceFromHalftime(match);
        var afterHalftime = new QuarterCheckpoint(match.Quarter, match.GameClockSeconds, match.Phase, match.OffenseDirection, match.PossessionTeam, match.BallSpot, match.MatchOver);

        match.GameClockSeconds = 1;
        match.Phase = MatchPhase.ThirdQuarter;
        play.Phase = PlayPhase.InPlay;
        for (var i = 0; i < 60; i++)
            clock.Update(match, play);
        var afterQ3 = new QuarterCheckpoint(match.Quarter, match.GameClockSeconds, match.Phase, match.OffenseDirection, match.PossessionTeam, match.BallSpot, match.MatchOver);

        match.GameClockSeconds = 1;
        match.Phase = MatchPhase.FourthQuarter;
        play.Phase = PlayPhase.InPlay;
        for (var i = 0; i < 60; i++)
            clock.Update(match, play);
        var final = new QuarterCheckpoint(match.Quarter, match.GameClockSeconds, match.Phase, match.OffenseDirection, match.PossessionTeam, match.BallSpot, match.MatchOver);

        return new QuarterFlowScenarioSummary(afterQ1, halftime, afterHalftime, afterQ3, final);
    }

    private static bool ValidateQuarterFlowScenario(QuarterFlowScenarioSummary summary, out string error)
    {
        error = string.Empty;

        if (summary.AfterQ1.Quarter != 2 || summary.AfterQ1.Phase != MatchPhase.SecondQuarter || summary.AfterQ1.Direction != OffenseDirection.RightToLeft)
        {
            error = $"expected Q2 + side change after Q1, got {summary.AfterQ1}";
            return false;
        }

        if (summary.Halftime.Phase != MatchPhase.Halftime || summary.Halftime.PossessionTeam != 0 || !summary.Halftime.BallSpot.Equals(BallSpot.Own(25)))
        {
            error = $"expected halftime reset/setup for deferred receiver, got {summary.Halftime}";
            return false;
        }

        if (summary.AfterHalftime.Quarter != 3 || summary.AfterHalftime.Phase != MatchPhase.ThirdQuarter || summary.AfterHalftime.Direction != OffenseDirection.LeftToRight)
        {
            error = $"expected Q3 resume from halftime, got {summary.AfterHalftime}";
            return false;
        }

        if (summary.AfterQ3.Quarter != 4 || summary.AfterQ3.Phase != MatchPhase.FourthQuarter || summary.AfterQ3.Direction != OffenseDirection.RightToLeft)
        {
            error = $"expected Q4 + side change after Q3, got {summary.AfterQ3}";
            return false;
        }

        if (summary.Final.Phase != MatchPhase.Final || !summary.Final.MatchOver)
        {
            error = $"expected final game-over state, got {summary.Final}";
            return false;
        }

        return true;
    }

    private static KickoffAfterScoreScenarioSummary? ExecuteKickoffAfterScoreScenario()
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 2,
            YardsToGo = 4,
            BallSpot = BallSpot.Opp(4),
            PlayNumber = 6,
            DriveId = 1,
        };

        var play = new PlayState();
        var kickoffAfterScore = new KickoffAfterScoreSystem(match, play);
        var lifecycle = new PlayLifecycleSystem(match, play, kickoffAfterScore);
        var downDistance = new DownDistanceSystem();

        var selected = new PlaySelectedEvent("00", "TD", "TD", 10, string.Empty);
        SimEventBus.Send(ref selected);
        lifecycle.Update(world);
        var scoringPlayId = play.PlayId;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Touchdown, endAbsoluteYard: 100, yardsGained: 4, turnover: false, touchdown: true, safety: false);

        var kickoffEvents = SimEventBus.Drain<KickoffSetupEvent>();
        if (kickoffEvents.Count <= 0)
            return null;

        var kickoff = kickoffEvents[^1];
        var snapshot = Snapshot(match, play);
        return new KickoffAfterScoreScenarioSummary(scoringPlayId, kickoff, snapshot, match.PendingKickoffReason, play.StartAbsoluteYard);
    }

    private static bool ValidateKickoffAfterScoreScenario(KickoffAfterScoreScenarioSummary summary, out string error)
    {
        error = string.Empty;

        if (summary.ScoringPlayId <= 0)
        {
            error = $"expected positive scoring play id, got {summary.ScoringPlayId}";
            return false;
        }

        if (summary.KickoffSetup.KickingTeam != 0 || summary.KickoffSetup.ReceivingTeam != 1 || summary.KickoffSetup.Reason != KickoffSetupReason.AfterTouchdown)
        {
            error = $"unexpected kickoff event {summary.KickoffSetup}";
            return false;
        }

        if (summary.PendingKickoffReason != KickoffSetupReason.AfterTouchdown)
        {
            error = $"expected pending kickoff reason AfterTouchdown got {summary.PendingKickoffReason}";
            return false;
        }

        if (summary.Snapshot.Team0Score != 6 || summary.Snapshot.Team1Score != 0 || summary.Snapshot.PossessionTeam != 0 || summary.Snapshot.Down != 1 || summary.Snapshot.YardsToGo != 10 || summary.Snapshot.GoalToGo || !summary.Snapshot.BallSpot.Equals(BallSpot.Own(25)) || summary.Snapshot.WhistleReason != WhistleReason.Touchdown || summary.Snapshot.PlayNumber != summary.ScoringPlayId)
        {
            error = $"kickoff setup snapshot wrong {summary.Snapshot}";
            return false;
        }

        if (summary.NextPlayStartAbsoluteYard != MatchState.TouchbackSpotYard)
        {
            error = $"expected kickoff next play start abs {MatchState.TouchbackSpotYard} got {summary.NextPlayStartAbsoluteYard}";
            return false;
        }

        return true;
    }

    private static KickoffScenarioSummary? ExecuteKickoffScenario(bool touchback)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 1,
            OffenseDirection = OffenseDirection.RightToLeft,
            Down = 1,
            YardsToGo = 10,
            BallSpot = BallSpot.Own(25),
            PlayNumber = 0,
            DriveId = 0,
            Phase = MatchPhase.FirstQuarter,
        };
        match.ResetForKickoff(kickingTeam: 1, receivingTeam: 0, reason: KickoffSetupReason.AfterTouchdown);
        if (touchback)
            match.KickoffLandingAbsoluteYardOverride = 0;
        else
            match.KickoffLandingAbsoluteYardOverride = 18;

        var play = new PlayState();
        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();
        var kickoff = new KickoffPlaySystem();
        var playerControl = new PlayerControlSystem();
        var speedModifiers = new SpeedModifierSystem();
        var movement = new MovementSystem();
        var contacts = new CollisionContactSystem();
        var tackle = new TackleResolutionSystem();
        var ballSystem = new BallSystem();
        var loosePickup = new LooseBallPickupSystem();
        var playResult = new PlayResultResolver(match, play);

        var offenseIds = new List<int>(11);
        var defenseIds = new List<int>(11);
        var control = new Control { ControlledEntityId = -1, PendingForcedEntityId = -1, PreviousControlledEntityId = -1 };

        var receivingIds = new List<int>(11);
        var kickingIds = new List<int>(11);
        var receivingRoles = new[] { RoleId.HB, RoleId.FB, RoleId.WR1, RoleId.WR2, RoleId.TE, RoleId.OC, RoleId.LG, RoleId.RG, RoleId.LT, RoleId.RT, RoleId.QB };
        var receivingKinds = new[] { PlayerRoleKind.RB, PlayerRoleKind.RB, PlayerRoleKind.WR, PlayerRoleKind.WR, PlayerRoleKind.TE, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.QB };
        var kickingRoles = new[] { RoleId.QB, RoleId.CB1, RoleId.CB2, RoleId.S1, RoleId.S2, RoleId.LB1, RoleId.LB2, RoleId.LB3, RoleId.LB4, RoleId.DL1, RoleId.DL2 };
        var kickingKinds = new[] { PlayerRoleKind.K, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.DL, PlayerRoleKind.DL };
        for (var i = 0; i < 11; i++)
        {
            receivingIds.Add(CreatePlayer(world, new Vector2(40f + i, 60f + (i * 10f % 100f)), teamIndex: 0, isOffense: true, receivingRoles[i], receivingKinds[i], $"R{i}", new Ratings { HP = 58, RS = 55, MS = 60 }));
            kickingIds.Add(CreatePlayer(world, new Vector2(180f + i, 60f + (i * 10f % 100f)), teamIndex: 1, isOffense: false, kickingRoles[i], kickingKinds[i], $"K{i}", new Ratings { HP = 60, RS = 58, MS = 62 }));
        }
        var ballId = BallEntityFactory.CreateBall(world, new Vector2(128f, 112f));

        var selected = new PlaySelectedEvent("KO", "KICKOFF", "KICKOFF", 0, string.Empty);
        SimEventBus.Send(ref selected);
        lifecycle.Update(world);

        kickoff.UpdatePreMovement(world, match, play, ballId, offenseIds, defenseIds, ref control);

        var snap = new SnapEvent(match.ReceivingTeamIndex, match.KickingTeamIndex);
        SimEventBus.Send(ref snap);
        lifecycle.Update(world);

        var forcedTackle = false;
        for (var tick = 0; tick < 360; tick++)
        {
            kickoff.UpdatePreMovement(world, match, play, ballId, offenseIds, defenseIds, ref control);
            playerControl.Update(world, DtSeconds, ballId, ref control);
            speedModifiers.Update(world, DtSeconds);
            movement.Update(world, DtSeconds, control.ControlledEntityId, Vector2.Zero);
            contacts.Update(world, offenseIds, defenseIds);
            tackle.Update(world, DtSeconds, ballId, ref control, play);
            ballSystem.Update(world, DtSeconds);
            kickoff.UpdatePostBall(world, match, play, ballId, ref control);
            loosePickup.Update(world, ballId, ref control, match, play);

            var ball = GetBall(world, ballId);
            if (!touchback && !forcedTackle && ball.State == BallState.Held && ball.OwnerEntityId > 0 && tick > 90)
            {
                playResult.ResolveOnTackle(world, ballId);
                SetBallDead(world, ballId, GetPosition(world, ballId));
                var ended = new PlayEndedEvent(
                    PlayId: play.PlayId,
                    Reason: (int)WhistleReason.Tackle,
                    EndAbsoluteYard: play.EndAbsoluteYard,
                    YardsGained: play.Result.YardsGained,
                    Turnover: play.Result.Turnover,
                    Touchdown: false,
                    Safety: false);
                SimEventBus.Send(ref ended);
                forcedTackle = true;
            }

            if (loosePickup.RecoveredThisTick)
            {
                playResult.ResolveOnTackle(world, ballId);
                var reason = loosePickup.TurnoverThisTick ? WhistleReason.Turnover : WhistleReason.Tackle;
                var ended = new PlayEndedEvent(
                    PlayId: play.PlayId,
                    Reason: (int)reason,
                    EndAbsoluteYard: play.EndAbsoluteYard,
                    YardsGained: play.Result.YardsGained,
                    Turnover: play.Result.Turnover,
                    Touchdown: false,
                    Safety: false);
                SimEventBus.Send(ref ended);
            }

            if (tackle.WhistledThisTick)
            {
                playResult.ResolveOnTackle(world, ballId);
                var ended = new PlayEndedEvent(
                    PlayId: play.PlayId,
                    Reason: (int)WhistleReason.Tackle,
                    EndAbsoluteYard: play.EndAbsoluteYard,
                    YardsGained: play.Result.YardsGained,
                    Turnover: play.Result.Turnover,
                    Touchdown: false,
                    Safety: false);
                SimEventBus.Send(ref ended);
            }

            lifecycle.Update(world);
            if (play.IsOver)
            {
                downDistance.ApplyPlayEnd(match, play);
                var finalBall = GetBall(world, ballId);
                return new KickoffScenarioSummary(touchback, Snapshot(match, play), control.ControlledEntityId, finalBall.OwnerEntityId, finalBall.State, forcedTackle);
            }
        }

        return null;
    }

    private static bool ValidateKickoffScenario(KickoffScenarioSummary summary, bool touchback, out string error)
    {
        error = string.Empty;

        if (touchback)
        {
            if (summary.Snapshot.WhistleReason != WhistleReason.Touchback)
            {
                error = $"expected touchback whistle got {summary.Snapshot.WhistleReason}";
                return false;
            }

            if (summary.Snapshot.PossessionTeam != 0 || summary.Snapshot.Down != 1 || summary.Snapshot.YardsToGo != 10 || !summary.Snapshot.BallSpot.Equals(BallSpot.Own(25)) || summary.BallState != BallState.Dead)
            {
                error = $"touchback snapshot wrong {summary}";
                return false;
            }

            return true;
        }

        if (summary.Snapshot.WhistleReason != WhistleReason.Tackle)
        {
            error = $"expected tackle whistle got {summary.Snapshot.WhistleReason}";
            return false;
        }

        if (summary.Snapshot.PossessionTeam != 0 || summary.Snapshot.Down != 1 || summary.Snapshot.YardsToGo != 10 || summary.Snapshot.BallSpot.Equals(BallSpot.Own(25)) || summary.BallState != BallState.Dead || !summary.ForcedTackle)
        {
            error = $"return snapshot wrong {summary}";
            return false;
        }

        return true;
    }

    private static PuntScenarioSummary? ExecutePuntScenario(PuntScenarioKind kind)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 4,
            YardsToGo = 8,
            BallSpot = BallSpot.Own(35),
            PlayNumber = 0,
            DriveId = 0,
        };
        match.PuntPending = true;
        match.PuntLandingAbsoluteYardOverride = kind switch
        {
            PuntScenarioKind.StandardReturn => 72,
            PuntScenarioKind.Downed => 90,
            PuntScenarioKind.Muff => 68,
            _ => 100,
        };
        match.ForcePuntMuff = kind == PuntScenarioKind.Muff;

        var play = new PlayState();
        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();
        var punt = new PuntPlaySystem();
        var playerControl = new PlayerControlSystem();
        var speedModifiers = new SpeedModifierSystem();
        var movement = new MovementSystem();
        var contacts = new CollisionContactSystem();
        var tackle = new TackleResolutionSystem();
        var ballSystem = new BallSystem();
        var loosePickup = new LooseBallPickupSystem();
        var playResult = new PlayResultResolver(match, play);

        var offenseIds = new List<int>(11);
        var defenseIds = new List<int>(11);
        var control = new Control { ControlledEntityId = -1, PendingForcedEntityId = -1, PreviousControlledEntityId = -1 };

        var puntRoles = new[] { RoleId.QB, RoleId.WR1, RoleId.WR2, RoleId.TE, RoleId.OC, RoleId.LG, RoleId.RG, RoleId.LT, RoleId.RT, RoleId.HB, RoleId.FB };
        var puntKinds = new[] { PlayerRoleKind.K, PlayerRoleKind.WR, PlayerRoleKind.WR, PlayerRoleKind.TE, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.OL, PlayerRoleKind.RB, PlayerRoleKind.RB };
        var returnRoles = new[] { RoleId.HB, RoleId.CB1, RoleId.CB2, RoleId.S1, RoleId.S2, RoleId.LB1, RoleId.LB2, RoleId.LB3, RoleId.LB4, RoleId.DL1, RoleId.DL2 };
        var returnKinds = new[] { PlayerRoleKind.RB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.DB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.LB, PlayerRoleKind.DL, PlayerRoleKind.DL };
        for (var i = 0; i < 11; i++)
        {
            _ = CreatePlayer(world, new Vector2(40f + i, 60f + (i * 10f % 100f)), teamIndex: 0, isOffense: false, puntRoles[i], puntKinds[i], $"P{i}", new Ratings { HP = 58, RS = 55, MS = 60 });
            _ = CreatePlayer(world, new Vector2(180f + i, 60f + (i * 10f % 100f)), teamIndex: 1, isOffense: true, returnRoles[i], returnKinds[i], $"R{i}", new Ratings { HP = 60, RS = 58, MS = 62 });
        }
        var ballId = BallEntityFactory.CreateBall(world, new Vector2(128f, 112f));

        var selected = new PlaySelectedEvent("PUNT", "PUNT", "PUNT", 0, string.Empty);
        SimEventBus.Send(ref selected);
        lifecycle.Update(world);
        punt.UpdatePreMovement(world, match, play, ballId, offenseIds, defenseIds, ref control);

        var snap = new SnapEvent(match.PossessionTeam, 1 - match.PossessionTeam);
        SimEventBus.Send(ref snap);
        lifecycle.Update(world);

        var forcedTackle = false;
        for (var tick = 0; tick < 360; tick++)
        {
            punt.UpdatePreMovement(world, match, play, ballId, offenseIds, defenseIds, ref control);
            playerControl.Update(world, DtSeconds, ballId, ref control);
            speedModifiers.Update(world, DtSeconds);
            movement.Update(world, DtSeconds, control.ControlledEntityId, Vector2.Zero);
            contacts.Update(world, offenseIds, defenseIds);
            tackle.Update(world, DtSeconds, ballId, ref control, play);
            ballSystem.Update(world, DtSeconds);
            punt.UpdatePostBall(world, match, play, ballId, ref control);
            loosePickup.Update(world, ballId, ref control, match, play);

            var ball = GetBall(world, ballId);
            if (kind == PuntScenarioKind.StandardReturn && !forcedTackle && ball.State == BallState.Held && ball.OwnerEntityId > 0 && tick > 90)
            {
                playResult.ResolveOnTackle(world, ballId);
                SetBallDead(world, ballId, GetPosition(world, ballId));
                var ended = new PlayEndedEvent(play.PlayId, (int)WhistleReason.Tackle, play.EndAbsoluteYard, play.Result.YardsGained, play.Result.Turnover, false, false);
                SimEventBus.Send(ref ended);
                forcedTackle = true;
            }

            if (loosePickup.RecoveredThisTick)
            {
                playResult.ResolveOnTackle(world, ballId);
                var reason = loosePickup.TurnoverThisTick ? WhistleReason.Turnover : WhistleReason.Tackle;
                var ended = new PlayEndedEvent(play.PlayId, (int)reason, play.EndAbsoluteYard, play.Result.YardsGained, play.Result.Turnover, false, false);
                SimEventBus.Send(ref ended);
            }

            if (tackle.WhistledThisTick)
            {
                playResult.ResolveOnTackle(world, ballId);
                var ended = new PlayEndedEvent(play.PlayId, (int)WhistleReason.Tackle, play.EndAbsoluteYard, play.Result.YardsGained, play.Result.Turnover, false, false);
                SimEventBus.Send(ref ended);
            }

            lifecycle.Update(world);
            if (play.IsOver)
            {
                downDistance.ApplyPlayEnd(match, play);
                var finalBall = GetBall(world, ballId);
                return new PuntScenarioSummary(kind, Snapshot(match, play), finalBall.OwnerEntityId, finalBall.State, forcedTackle);
            }

            if (kind is PuntScenarioKind.Downed or PuntScenarioKind.Muff && !play.IsOver && tick > 220)
            {
                playResult.ResolveOnTackle(world, ballId);
                SetBallDead(world, ballId, GetPosition(world, ballId));
                var ended = new PlayEndedEvent(play.PlayId, (int)WhistleReason.Turnover, play.EndAbsoluteYard, play.Result.YardsGained, true, false, false);
                SimEventBus.Send(ref ended);
            }
        }

        return null;
    }

    private static bool ValidatePuntScenario(PuntScenarioSummary summary, PuntScenarioKind kind, out string error)
    {
        error = string.Empty;
        switch (kind)
        {
            case PuntScenarioKind.StandardReturn:
                if (summary.Snapshot.WhistleReason != WhistleReason.Tackle || summary.Snapshot.PossessionTeam != 1 || summary.Snapshot.BallSpot.Equals(BallSpot.Own(25)) || summary.BallState != BallState.Dead || !summary.ForcedTackle)
                {
                    error = $"standard return wrong {summary}";
                    return false;
                }
                return true;
            case PuntScenarioKind.Touchback:
                if (summary.Snapshot.WhistleReason != WhistleReason.Touchback || summary.Snapshot.PossessionTeam != 1 || !summary.Snapshot.BallSpot.Equals(BallSpot.Own(25)) || summary.BallState != BallState.Dead)
                {
                    error = $"touchback wrong {summary}";
                    return false;
                }
                return true;
            case PuntScenarioKind.Downed:
                if (summary.Snapshot.WhistleReason != WhistleReason.Turnover || summary.Snapshot.PossessionTeam != 1 || summary.BallState != BallState.Dead)
                {
                    error = $"downed wrong {summary}";
                    return false;
                }
                return true;
            case PuntScenarioKind.Muff:
                if (summary.Snapshot.WhistleReason is not (WhistleReason.Turnover or WhistleReason.Tackle) || summary.Snapshot.PossessionTeam != 1 || summary.BallState != BallState.Dead)
                {
                    error = $"muff wrong {summary}";
                    return false;
                }
                return true;
            default:
                error = $"unknown punt kind {kind}";
                return false;
        }
    }

    private static ScoreboardIntegrationScenarioSummary? ExecuteScoreboardIntegrationScenario()
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var match = new MatchState
        {
            Quarter = 1,
            GameClockSeconds = 300,
            PossessionTeam = 0,
            OffenseDirection = OffenseDirection.LeftToRight,
            Down = 3,
            YardsToGo = 2,
            GoalToGo = true,
            BallSpot = BallSpot.Opp(2),
            PlayNumber = 0,
            DriveId = 0,
        };

        var play = new PlayState();
        var lifecycle = new PlayLifecycleSystem(match, play);
        var downDistance = new DownDistanceSystem();

        var selected = new PlaySelectedEvent("00", "SCORE", "SCORE", 10, string.Empty);
        SimEventBus.Send(ref selected);
        lifecycle.Update(world);
        var goalPlayId = play.PlayId;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Tackle, endAbsoluteYard: 99, yardsGained: 1, turnover: false, touchdown: false, safety: false);
        var afterGoalShort = Snapshot(match, play);

        var continue1 = new PostPlayContinueRequestedEvent();
        SimEventBus.Send(ref continue1);
        lifecycle.Update(world);

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Turnover, endAbsoluteYard: 99, yardsGained: 0, turnover: false, touchdown: false, safety: false);
        var afterTurnoverOnDowns = Snapshot(match, play);

        var continue2 = new PostPlayContinueRequestedEvent();
        SimEventBus.Send(ref continue2);
        lifecycle.Update(world);

        match.PossessionTeam = 0;
        match.OffenseDirection = OffenseDirection.LeftToRight;
        match.BallSpot = BallSpot.Opp(8);
        match.Down = 1;
        match.YardsToGo = 8;
        match.GoalToGo = true;
        match.DriveId = 2;
        play.ResetForNewPlay(play.PlayId, PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection));
        play.Phase = PlayPhase.InPlay;

        CompleteDrivePlay(world, lifecycle, downDistance, match, play, WhistleReason.Touchdown, endAbsoluteYard: 100, yardsGained: 8, turnover: false, touchdown: true, safety: false);
        var afterTouchdown = Snapshot(match, play);

        return new ScoreboardIntegrationScenarioSummary(goalPlayId, afterGoalShort, afterTurnoverOnDowns, afterTouchdown);
    }

    private static bool ValidateScoreboardIntegrationScenario(ScoreboardIntegrationScenarioSummary summary, out string error)
    {
        error = string.Empty;

        if (summary.AfterGoalShort.PossessionTeam != 0 || summary.AfterGoalShort.Down != 4 || !summary.AfterGoalShort.GoalToGo || !summary.AfterGoalShort.BallSpot.Equals(BallSpot.Opp(1)) || summary.AfterGoalShort.YardsToGo != 1)
        {
            error = $"goal-to-go checkpoint wrong {summary.AfterGoalShort}";
            return false;
        }

        if (summary.AfterTurnoverOnDowns.PossessionTeam != 1 || summary.AfterTurnoverOnDowns.Down != 1 || summary.AfterTurnoverOnDowns.YardsToGo != 10 || summary.AfterTurnoverOnDowns.GoalToGo || !summary.AfterTurnoverOnDowns.BallSpot.Equals(BallSpot.Own(1)) || summary.AfterTurnoverOnDowns.OffenseDirection != OffenseDirection.RightToLeft || summary.AfterTurnoverOnDowns.DriveId != 1)
        {
            error = $"turnover-on-downs checkpoint wrong {summary.AfterTurnoverOnDowns}";
            return false;
        }

        if (summary.AfterTouchdown.Team0Score != 6 || summary.AfterTouchdown.Team1Score != 0 || summary.AfterTouchdown.PossessionTeam != 0 || summary.AfterTouchdown.Down != 1 || summary.AfterTouchdown.YardsToGo != 10 || summary.AfterTouchdown.GoalToGo || !summary.AfterTouchdown.BallSpot.Equals(BallSpot.Own(25)))
        {
            error = $"touchdown scoreboard checkpoint wrong {summary.AfterTouchdown}";
            return false;
        }

        return true;
    }

    private static int CreatePlayer(World world, Vector2 position, int teamIndex, bool isOffense, RoleId roleId, PlayerRoleKind playerRoleKind, string slot, Ratings ratings)
    {
        var id = PlayerEntityFactory.CreatePlayer(world, position, teamIndex, isPlayerControlled: false, isOffense: isOffense);
        WithEntity(world, id, e =>
        {
            e.Add(new Role { Id = roleId });
            e.Add(PlayerRole.Create(playerRoleKind, slot));
            e.Add(ratings);
        });
        return id;
    }

    private static void SetPosition(World world, int entityId, Vector2 position)
    {
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position pos) =>
        {
            if (e.Id != entityId)
                return;

            pos.Value = position;
        });

        var qVel = new QueryDescription().WithAll<Velocity>();
        world.Query(in qVel, (Entity e, ref Velocity vel) =>
        {
            if (e.Id != entityId)
                return;

            vel.Value = Vector2.Zero;
        });
    }

    private static Vector2 GetPosition(World world, int entityId)
    {
        var found = false;
        var result = Vector2.Zero;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position pos) =>
        {
            if (found || e.Id != entityId)
                return;

            result = pos.Value;
            found = true;
        });
        return result;
    }

    private static void SetBallHeld(World world, int ballId, int ownerId)
    {
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball ball) =>
        {
            if (e.Id != ballId)
                return;

            ball.State = BallState.Held;
            ball.OwnerEntityId = ownerId;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = false;
            ball.Height = 0f;
        });
        SetPosition(world, ballId, GetPosition(world, ownerId));
    }

    private static void SetBallDead(World world, int ballId, Vector2 position)
    {
        var q = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in q, (Entity e, ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballId)
                return;

            ball.State = BallState.Dead;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = true;
            ball.Height = 0f;
            pos.Value = position;
            vel.Value = Vector2.Zero;
        });
    }

    private static Ball GetBall(World world, int ballId)
    {
        var found = false;
        var result = default(Ball);
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball ball) =>
        {
            if (found || e.Id != ballId)
                return;

            result = ball;
            found = true;
        });
        return result;
    }

    private static void SetQbBrain(World world, int entityId, QbBrain qbBrain)
    {
        WithEntity(world, entityId, e =>
        {
            if (e.Has<QbBrain>())
                e.Set(qbBrain);
            else
                e.Add(qbBrain);
        });
    }

    private static void SetCoverage(World world, int entityId, Coverage coverage)
    {
        WithEntity(world, entityId, e =>
        {
            if (e.Has<Coverage>())
                e.Set(coverage);
            else
                e.Add(coverage);
        });
    }

    private static Coverage GetCoverage(World world, int entityId)
    {
        var result = Coverage.Default;
        var found = false;
        var q = new QueryDescription().WithAll<Coverage>();
        world.Query(in q, (Entity e, ref Coverage coverage) =>
        {
            if (found || e.Id != entityId)
                return;
            result = coverage;
            found = true;
        });
        return result;
    }

    private static void SetRush(World world, int entityId, Rush rush)
    {
        WithEntity(world, entityId, e =>
        {
            if (e.Has<Rush>())
                e.Set(rush);
            else
                e.Add(rush);
        });
    }

    private static void WithEntity(World world, int entityId, Action<Entity> action)
    {
        var handled = false;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position _) =>
        {
            if (handled || e.Id != entityId)
                return;

            action(e);
            handled = true;
        });

        if (!handled)
            throw new InvalidOperationException($"Entity {entityId} not found");
    }

    private static void DrainResidualEvents()
    {
        _ = SimEventBus.Drain<PassRequestedEvent>();
        _ = SimEventBus.Drain<BallCaughtEvent>();
        _ = SimEventBus.Drain<PassResolvedEvent>();
        _ = SimEventBus.Drain<WhistleEvent>();
        _ = SimEventBus.Drain<PlayEndedEvent>();
        _ = SimEventBus.Drain<TackleContactEvent>();
        _ = SimEventBus.Drain<TackleResolvedEvent>();
        _ = SimEventBus.Drain<FumbleEvent>();
        _ = SimEventBus.Drain<LooseBallPickupEvent>();
        _ = SimEventBus.Drain<PostPlayContinueRequestedEvent>();
        _ = SimEventBus.Drain<PlaySelectedEvent>();
        _ = SimEventBus.Drain<SnapEvent>();
        _ = SimEventBus.Drain<KickoffSetupEvent>();
    }

    private enum PuntScenarioKind
    {
        StandardReturn = 0,
        Touchback = 1,
        Downed = 2,
        Muff = 3,
    }

    private enum FieldGoalScenarioKind
    {
        GoodFieldGoal = 0,
        MissedFieldGoal = 1,
        BlockedExtraPoint = 2,
        GoodExtraPoint = 3,
    }

    private static PressureScenarioSummary? ExecutePressureCase(int blockerCount, int ticks)
    {
        DrainResidualEvents();

        using var world = World.Create();
        _ = world.Create();

        var qbPos = new Vector2(120f, 112f);
        var qbId = CreatePlayer(world, qbPos, teamIndex: 0, isOffense: true, RoleId.QB, PlayerRoleKind.QB, "QB", new Ratings { HP = 55, RS = 45, MS = 45 });
        var rusherId = CreatePlayer(world, qbPos + new Vector2(10f, 16f), teamIndex: 1, isOffense: false, RoleId.DL4, PlayerRoleKind.DL, "DE-R", new Ratings { HP = 72, RS = 60, MS = 68 });
        var ballId = BallEntityFactory.CreateBall(world, qbPos);
        SetBallHeld(world, ballId, qbId);
        SetQbBrain(world, qbId, QbBrain.Default with { DropbackFramesRemaining = 12, ReadTimeLimitFrames = 8, PressureThresholdFrames = 4 });
        SetRush(world, rusherId, new Rush
        {
            Assignment = RushAssignment.EdgeRight,
            HasLandmark = true,
            Landmark = qbPos + new Vector2(8f, 16f),
            ReachedLandmark = true,
            TargetGap = RushGap.ContainRight,
            IsContain = true,
            Type = RushType.Swim,
            Engaged = false,
            EngagedBlockerId = -1,
        });

        var offenseIds = new List<int> { qbId };
        var defenseIds = new List<int> { rusherId };

        if (blockerCount >= 1)
            offenseIds.Add(CreatePressureBlocker(world, qbPos + new Vector2(10f, 14f), RoleId.RT, "RT", new Ratings { HP = 82, RS = 70, MS = 52 }, preferredKey: "RE"));
        if (blockerCount >= 2)
            offenseIds.Add(CreatePressureBlocker(world, qbPos + new Vector2(10f, 20f), RoleId.TE, "TE", new Ratings { HP = 84, RS = 70, MS = 50 }, preferredKey: "RE"));

        var blockerAi = new BlockerAiSystem();
        var rush = new DefensiveRushSystem();
        var speedModifiers = new SpeedModifierSystem();
        var movement = new MovementSystem();
        var contacts = new CollisionContactSystem();
        var engagement = new EngagementSystem();
        var behaviorStack = new BehaviorStackSystem();

        var activePressureTicks = 0;
        var maxPressureFrames = 0;
        var sawDoubleTeam = false;
        var engagementStarts = 0;
        var helperAssignments = 0;

        for (var tick = 0; tick < ticks; tick++)
        {
            blockerAi.Update(world, DtSeconds, offenseIds, defenseIds, ballId);
            rush.Update(world, DtSeconds, defenseIds);
            speedModifiers.Update(world, DtSeconds);
            movement.Update(world, DtSeconds, controlledEntityId: -1, inputDir: Vector2.Zero);
            contacts.Update(world, offenseIds, defenseIds);
            engagement.Update(world, DtSeconds);
            behaviorStack.Update(world, DtSeconds);

            var qbBrain = GetQbBrain(world, qbId);
            if (qbBrain.PressureDetected)
                activePressureTicks++;
            maxPressureFrames = Math.Max(maxPressureFrames, qbBrain.PressureFrameCount);
            sawDoubleTeam |= IsDoubleTeamed(world, rusherId);
            if (IsEngagedWithMultipleHelpers(world, rusherId))
                engagementStarts++;
            helperAssignments = Math.Max(helperAssignments, CountHelpers(world, rusherId));
        }

        DrainResidualEvents();
        return new PressureScenarioSummary(blockerCount, activePressureTicks, maxPressureFrames, sawDoubleTeam, engagementStarts, helperAssignments);
    }

    private static int CreatePressureBlocker(World world, Vector2 position, RoleId roleId, string slot, Ratings ratings, string preferredKey)
    {
        var blockerId = CreatePlayer(world, position, teamIndex: 0, isOffense: true, roleId, PlayerRoleKind.OL, slot, ratings);
        WithEntity(world, blockerId, e =>
        {
            e.Add(new BlockTarget
            {
                TargetEntityId = -1,
                Assignment = roleId is RoleId.LT or RoleId.LG ? BlockAssignmentType.GapLeft : BlockAssignmentType.GapRight,
                PreferredDefenderKey = preferredKey,
                IsEngaged = false,
                EngagedEntityId = -1,
                EngagementFrame = 0,
                IsDoubleTeam = false,
            });
        });
        return blockerId;
    }

    private static bool IsDoubleTeamed(World world, int defenderId)
    {
        var count = 0;
        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity _, ref BlockTarget blockTarget) =>
        {
            if (blockTarget.IsDoubleTeam && blockTarget.EngagedEntityId == defenderId)
                count++;
        });

        return count >= 2;
    }

    private static bool IsEngagedWithMultipleHelpers(World world, int defenderId)
        => CountHelpers(world, defenderId) >= 2;

    private static int CountHelpers(World world, int defenderId)
    {
        var count = 0;
        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity _, ref BlockTarget blockTarget) =>
        {
            if (blockTarget.TargetEntityId == defenderId || blockTarget.EngagedEntityId == defenderId)
                count++;
        });

        return count;
    }

    private static QbBrain GetQbBrain(World world, int qbId)
    {
        var result = QbBrain.Default;
        var q = new QueryDescription().WithAll<QbBrain>();
        world.Query(in q, (Entity e, ref QbBrain qbBrain) =>
        {
            if (e.Id == qbId)
                result = qbBrain;
        });
        return result;
    }

    private static bool TryNormalizeScenarioName(string scenarioName, out string normalizedScenario)
    {
        normalizedScenario = scenarioName.Trim().ToLowerInvariant();
        return normalizedScenario switch
        {
            "scrimmage-pack" or
            "pass-outcomes" or
            "drive" or
            "quarter-flow" or
            "scoreboard" or
            "fumble" or
            "stats" or
            "kickoff-after-score" or
            "kickoff" or
            "punt" or
            "field-goal" or
            "pressure" => true,
            _ => false,
        };
    }

    private static bool TryBuildDeterminismArtifact(string scenarioName, out string artifactJson, out string error)
    {
        error = string.Empty;
        artifactJson = string.Empty;

        object? artifact = scenarioName switch
        {
            "scrimmage-pack" => BuildScrimmagePackArtifact(out error),
            "pass-outcomes" => BuildPassOutcomesArtifact(out error),
            "drive" => BuildDriveArtifact(out error),
            "quarter-flow" => BuildQuarterFlowArtifact(out error),
            "scoreboard" => BuildScoreboardArtifact(out error),
            "fumble" => BuildFumbleArtifact(out error),
            "stats" => BuildStatsArtifact(out error),
            "kickoff-after-score" => BuildKickoffAfterScoreArtifact(out error),
            "kickoff" => BuildKickoffArtifact(out error),
            "punt" => BuildPuntArtifact(out error),
            "field-goal" => BuildFieldGoalArtifact(out error),
            "pressure" => BuildPressureArtifact(out error),
            _ => null,
        };

        if (artifact is null)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "artifact generation returned no result";
            return false;
        }

        artifactJson = JsonSerializer.Serialize(artifact, ArtifactJsonOptions);
        return true;
    }

    private static object? BuildScrimmagePackArtifact(out string error)
    {
        error = string.Empty;

        var drive = ExecuteDriveLifecycleScenario();
        if (drive is null || !ValidateDriveLifecycleScenario(drive.Value, out error))
            return null;

        var pass = BuildPassOutcomesArtifact(out error);
        if (pass is null)
            return null;

        var fumble = ExecuteFumbleScenario(defenseRecovers: false);
        var turnoverFumble = ExecuteFumbleScenario(defenseRecovers: true);
        if (fumble is null || turnoverFumble is null
            || !ValidateFumbleScenario(fumble.Value, defenseRecovers: false, out error)
            || !ValidateFumbleScenario(turnoverFumble.Value, defenseRecovers: true, out error))
            return null;

        var scoreboard = ExecuteScoreboardIntegrationScenario();
        if (scoreboard is null || !ValidateScoreboardIntegrationScenario(scoreboard.Value, out error))
            return null;

        var quarterFlow = ExecuteQuarterFlowScenario();
        if (quarterFlow is null || !ValidateQuarterFlowScenario(quarterFlow.Value, out error))
            return null;

        return new DeterminismArtifact(
            Scenario: "scrimmage-pack",
            Results: new List<DeterminismArtifactResult>
            {
                new("run-drive", drive.Value),
                new("pass-outcomes", pass),
                new("turnover-fumble", new { offenseRecovery = fumble.Value, defenseRecovery = turnoverFumble.Value }),
                new("scoring-scoreboard", scoreboard.Value),
                new("reset-quarter-flow", quarterFlow.Value),
            });
    }

    private static object? BuildPassOutcomesArtifact(out string error)
    {
        error = string.Empty;
        var results = new List<PassScenarioSummary>();
        foreach (var scenario in new[]
                 {
                     PassScenarioKind.Completion,
                     PassScenarioKind.Incomplete,
                     PassScenarioKind.Interception,
                     PassScenarioKind.CoverageBreakup,
                     PassScenarioKind.PressureThrow,
                 })
        {
            var summary = ExecutePassScenario(scenario);
            if (summary is null)
            {
                error = $"scenario={scenario} did not resolve";
                return null;
            }

            if (!ValidateScenario(summary.Value, scenario, out error))
                return null;

            results.Add(summary.Value);
        }

        return new DeterminismArtifact("pass-outcomes", new List<DeterminismArtifactResult> { new("pass-outcomes", results) });
    }

    private static object? BuildDriveArtifact(out string error)
    {
        error = string.Empty;
        var summary = ExecuteDriveLifecycleScenario();
        if (summary is null || !ValidateDriveLifecycleScenario(summary.Value, out error))
            return null;
        return new DeterminismArtifact("drive", new List<DeterminismArtifactResult> { new("drive", summary.Value) });
    }

    private static object? BuildQuarterFlowArtifact(out string error)
    {
        error = string.Empty;
        var summary = ExecuteQuarterFlowScenario();
        if (summary is null || !ValidateQuarterFlowScenario(summary.Value, out error))
            return null;
        return new DeterminismArtifact("quarter-flow", new List<DeterminismArtifactResult> { new("quarter-flow", summary.Value) });
    }

    private static object? BuildScoreboardArtifact(out string error)
    {
        error = string.Empty;
        var summary = ExecuteScoreboardIntegrationScenario();
        if (summary is null || !ValidateScoreboardIntegrationScenario(summary.Value, out error))
            return null;
        return new DeterminismArtifact("scoreboard", new List<DeterminismArtifactResult> { new("scoreboard", summary.Value) });
    }

    private static object? BuildFumbleArtifact(out string error)
    {
        error = string.Empty;
        var offenseRecovery = ExecuteFumbleScenario(defenseRecovers: false);
        var defenseRecovery = ExecuteFumbleScenario(defenseRecovers: true);
        if (offenseRecovery is null || defenseRecovery is null
            || !ValidateFumbleScenario(offenseRecovery.Value, defenseRecovers: false, out error)
            || !ValidateFumbleScenario(defenseRecovery.Value, defenseRecovers: true, out error))
            return null;

        return new DeterminismArtifact(
            "fumble",
            new List<DeterminismArtifactResult>
            {
                new("offense-recovery", offenseRecovery.Value),
                new("defense-recovery", defenseRecovery.Value),
            });
    }

    private static object? BuildStatsArtifact(out string error)
    {
        error = string.Empty;
        var passing = StatsScenarioRunner.RunPassingScenario();
        var rushing = StatsScenarioRunner.RunRushingScenario();
        var turnover = StatsScenarioRunner.RunTurnoverScenario();
        if (!ValidatePassingStats(passing, out error)
            || !ValidateRushingStats(rushing, out error)
            || !ValidateTurnoverStats(turnover, out error))
            return null;

        return new DeterminismArtifact(
            "stats",
            new List<DeterminismArtifactResult>
            {
                new("passing", passing),
                new("rushing", rushing),
                new("turnover", turnover),
            });
    }

    private static object? BuildKickoffAfterScoreArtifact(out string error)
    {
        error = string.Empty;
        var summary = ExecuteKickoffAfterScoreScenario();
        if (summary is null || !ValidateKickoffAfterScoreScenario(summary.Value, out error))
            return null;
        return new DeterminismArtifact("kickoff-after-score", new List<DeterminismArtifactResult> { new("kickoff-after-score", summary.Value) });
    }

    private static object? BuildKickoffArtifact(out string error)
    {
        error = string.Empty;
        var returnSummary = ExecuteKickoffScenario(touchback: false);
        var touchbackSummary = ExecuteKickoffScenario(touchback: true);
        if (returnSummary is null || touchbackSummary is null
            || !ValidateKickoffScenario(returnSummary.Value, touchback: false, out error)
            || !ValidateKickoffScenario(touchbackSummary.Value, touchback: true, out error))
            return null;

        return new DeterminismArtifact(
            "kickoff",
            new List<DeterminismArtifactResult>
            {
                new("return", returnSummary.Value),
                new("touchback", touchbackSummary.Value),
            });
    }

    private static object? BuildPuntArtifact(out string error)
    {
        error = string.Empty;
        var standardReturn = ExecutePuntScenario(PuntScenarioKind.StandardReturn);
        var touchback = ExecutePuntScenario(PuntScenarioKind.Touchback);
        var downed = ExecutePuntScenario(PuntScenarioKind.Downed);
        var muff = ExecutePuntScenario(PuntScenarioKind.Muff);
        if (standardReturn is null || touchback is null || downed is null || muff is null)
        {
            error = "one or more punt scenarios did not resolve";
            return null;
        }

        if (!ValidatePuntScenario(standardReturn.Value, PuntScenarioKind.StandardReturn, out error)
            || !ValidatePuntScenario(touchback.Value, PuntScenarioKind.Touchback, out error)
            || !ValidatePuntScenario(downed.Value, PuntScenarioKind.Downed, out error)
            || !ValidatePuntScenario(muff.Value, PuntScenarioKind.Muff, out error))
            return null;

        return new DeterminismArtifact(
            "punt",
            new List<DeterminismArtifactResult>
            {
                new("return", standardReturn.Value),
                new("touchback", touchback.Value),
                new("downed", downed.Value),
                new("muff", muff.Value),
            });
    }

    private static object? BuildFieldGoalArtifact(out string error)
    {
        error = string.Empty;
        var goodFg = ExecuteFieldGoalScenario(FieldGoalScenarioKind.GoodFieldGoal);
        var missFg = ExecuteFieldGoalScenario(FieldGoalScenarioKind.MissedFieldGoal);
        var blockedPat = ExecuteFieldGoalScenario(FieldGoalScenarioKind.BlockedExtraPoint);
        var goodPat = ExecuteFieldGoalScenario(FieldGoalScenarioKind.GoodExtraPoint);
        if (goodFg is null || missFg is null || blockedPat is null || goodPat is null)
        {
            error = "one or more field-goal scenarios did not resolve";
            return null;
        }

        if (!ValidateFieldGoalScenario(goodFg.Value, FieldGoalScenarioKind.GoodFieldGoal, out error)
            || !ValidateFieldGoalScenario(missFg.Value, FieldGoalScenarioKind.MissedFieldGoal, out error)
            || !ValidateFieldGoalScenario(blockedPat.Value, FieldGoalScenarioKind.BlockedExtraPoint, out error)
            || !ValidateFieldGoalScenario(goodPat.Value, FieldGoalScenarioKind.GoodExtraPoint, out error))
            return null;

        return new DeterminismArtifact(
            "field-goal",
            new List<DeterminismArtifactResult>
            {
                new("good-field-goal", goodFg.Value),
                new("missed-field-goal", missFg.Value),
                new("blocked-extra-point", blockedPat.Value),
                new("good-extra-point", goodPat.Value),
            });
    }

    private static object? BuildPressureArtifact(out string error)
    {
        error = string.Empty;
        var free = ExecutePressureCase(blockerCount: 0, ticks: 120);
        var single = ExecutePressureCase(blockerCount: 1, ticks: 120);
        var doubleTeam = ExecutePressureCase(blockerCount: 2, ticks: 120);
        if (free is null || single is null || doubleTeam is null)
        {
            error = "one or more pressure scenarios did not resolve";
            return null;
        }

        if (free.Value.ActivePressureTicks <= single.Value.ActivePressureTicks)
        {
            error = $"expected single blocker to reduce pressure got free={free.Value} single={single.Value}";
            return null;
        }

        if (doubleTeam.Value.HelperAssignments < 2)
        {
            error = $"expected double-team case to keep two helpers on the rusher got {doubleTeam.Value}";
            return null;
        }

        if (single.Value.ActivePressureTicks <= doubleTeam.Value.ActivePressureTicks)
        {
            error = $"expected double team to reduce pressure more than single blocker got single={single.Value} double={doubleTeam.Value}";
            return null;
        }

        return new DeterminismArtifact(
            "pressure",
            new List<DeterminismArtifactResult>
            {
                new("free", free.Value),
                new("single", single.Value),
                new("double-team", doubleTeam.Value),
            });
    }

    private static string DescribeFirstDifference(string baselineJson, string artifactJson)
    {
        var baselineLines = baselineJson.Split('\n');
        var actualLines = artifactJson.Split('\n');
        var max = Math.Min(baselineLines.Length, actualLines.Length);
        for (var i = 0; i < max; i++)
        {
            if (!string.Equals(baselineLines[i], actualLines[i], StringComparison.Ordinal))
                return $"line {i + 1} baseline='{baselineLines[i]}' actual='{actualLines[i]}'";
        }

        return $"line-count baseline={baselineLines.Length} actual={actualLines.Length}";
    }

    private enum PassScenarioKind
    {
        Completion = 0,
        Incomplete = 1,
        Interception = 2,
        CoverageBreakup = 3,
        PressureThrow = 4,
    }

    private readonly record struct PassScenarioSummary(
        PassScenarioKind Scenario,
        PassOutcome Outcome,
        int LiveOwnerId,
        BallState FinalBallState,
        int FinalBallOwnerId,
        PlayPhase Phase,
        WhistleReason WhistleReason,
        bool Turnover,
        int YardsGained,
        int EndAbsoluteYard,
        int PossessionTeam,
        int Down,
        int YardsToGo,
        int DriveId,
        OffenseDirection OffenseDirection,
        int? TargetedReceiverId,
        bool PressureForcedRead);

    private readonly record struct PressureScenarioSummary(
        int BlockerCount,
        int ActivePressureTicks,
        int MaxPressureFrames,
        bool SawDoubleTeam,
        int EngagementStarts,
        int HelperAssignments);

    private readonly record struct FieldGoalScenarioSummary(
        FieldGoalScenarioKind Kind,
        DriveCheckpoint Snapshot,
        KickoffSetupReason? PendingKickoffReason,
        int Team0Score,
        int Team1Score);

    private readonly record struct FumbleScenarioSummary(
        bool RecoveredByDefense,
        int RecoveringPlayerId,
        int PossessionTeam,
        int Down,
        int YardsToGo,
        int DriveId,
        OffenseDirection OffenseDirection,
        WhistleReason WhistleReason,
        bool Turnover,
        int YardsGained,
        int EndAbsoluteYard,
        BallState FinalBallState,
        int FinalBallOwnerId,
        PlayPhase Phase);

    private readonly record struct DriveCheckpoint(
        int PossessionTeam,
        int Down,
        int YardsToGo,
        BallSpot BallSpot,
        int Team0Score,
        int Team1Score,
        int DriveId,
        int PlayNumber,
        OffenseDirection OffenseDirection,
        PlayPhase Phase,
        WhistleReason WhistleReason,
        int EndAbsoluteYard,
        bool GoalToGo);

    private readonly record struct DriveLifecycleScenarioSummary(
        int Play1StartId,
        int Play2StartId,
        int Play3StartId,
        int Play4StartId,
        DriveCheckpoint AfterRun,
        DriveCheckpoint AfterFirstDown,
        DriveCheckpoint AfterTouchdown,
        DriveCheckpoint AfterTurnover);

    private readonly record struct QuarterCheckpoint(
        int Quarter,
        int GameClockSeconds,
        MatchPhase Phase,
        OffenseDirection Direction,
        int PossessionTeam,
        BallSpot BallSpot,
        bool MatchOver);

    private readonly record struct QuarterFlowScenarioSummary(
        QuarterCheckpoint AfterQ1,
        QuarterCheckpoint Halftime,
        QuarterCheckpoint AfterHalftime,
        QuarterCheckpoint AfterQ3,
        QuarterCheckpoint Final);

    private readonly record struct KickoffAfterScoreScenarioSummary(
        int ScoringPlayId,
        KickoffSetupEvent KickoffSetup,
        DriveCheckpoint Snapshot,
        KickoffSetupReason? PendingKickoffReason,
        int NextPlayStartAbsoluteYard);

    private readonly record struct KickoffScenarioSummary(
        bool Touchback,
        DriveCheckpoint Snapshot,
        int ControlledEntityId,
        int FinalBallOwnerId,
        BallState BallState,
        bool ForcedTackle);

    private readonly record struct PuntScenarioSummary(
        PuntScenarioKind Kind,
        DriveCheckpoint Snapshot,
        int FinalBallOwnerId,
        BallState BallState,
        bool ForcedTackle);

    private readonly record struct DeterminismArtifact(
        string Scenario,
        List<DeterminismArtifactResult> Results);

    private readonly record struct DeterminismArtifactResult(
        string Name,
        object Payload);

    private static bool ValidatePassingStats(StatsScenarioSummary summary, out string error)
    {
        var team = summary.Stats.Match.GetTeam(0);
        if (team.PassAttempts != 1 || team.PassCompletions != 1 || team.PassingYards != 12)
        {
            error = $"expected team passing 1/1 for 12 got att={team.PassAttempts} comp={team.PassCompletions} yds={team.PassingYards}";
            return false;
        }

        if (summary.Replay.Events.Count < 2)
        {
            error = "expected replay events for pass completion";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateRushingStats(StatsScenarioSummary summary, out string error)
    {
        var team = summary.Stats.Match.GetTeam(0);
        if (team.RushingAttempts != 1 || team.RushingYards != 6)
        {
            error = $"expected team rushing 1 for 6 got att={team.RushingAttempts} yds={team.RushingYards}";
            return false;
        }

        if (summary.Replay.Events.Count != 1 || summary.Replay.Events[0].Type != "rush")
        {
            error = "expected single rush replay event";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateTurnoverStats(StatsScenarioSummary summary, out string error)
    {
        var offense = summary.Stats.Match.GetTeam(0);
        var defense = summary.Stats.Match.GetTeam(1);
        if (offense.TurnoversCommitted != 1 || defense.TurnoversForced != 1 || defense.Interceptions != 1)
        {
            error = $"expected turnover/interception counts got offTO={offense.TurnoversCommitted} defForced={defense.TurnoversForced} defInt={defense.Interceptions}";
            return false;
        }

        if (summary.Replay.Events.Count != 1 || summary.Replay.Events[0].Type != "interception" || !summary.Replay.Events[0].Turnover)
        {
            error = "expected interception replay event";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private readonly record struct ScoreboardIntegrationScenarioSummary(
        int GoalPlayId,
        DriveCheckpoint AfterGoalShort,
        DriveCheckpoint AfterTurnoverOnDowns,
        DriveCheckpoint AfterTouchdown);
}
