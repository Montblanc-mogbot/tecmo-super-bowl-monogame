using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.Factories;
using TecmoSBGame.SimArch.State;
using TecmoSBGame.SimArch.Systems;

namespace TecmoSBGame.SimArch;

public static class SimArchHeadless
{
    private const float DtSeconds = 1f / 60f;

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
        => new(match.PossessionTeam, match.Down, match.YardsToGo, match.BallSpot, match.Team0Score, match.Team1Score, match.DriveId, match.PlayNumber, match.OffenseDirection, play.Phase, play.WhistleReason, play.EndAbsoluteYard);

    private static bool ValidateDriveLifecycleScenario(DriveLifecycleScenarioSummary summary, out string error)
    {
        error = string.Empty;

        if (summary.Play1StartId <= 0 || summary.Play2StartId != summary.Play1StartId + 1 || summary.Play3StartId != summary.Play2StartId + 1 || summary.Play4StartId != summary.Play3StartId + 1)
        {
            error = $"expected monotonic +1 play id progression got {summary.Play1StartId},{summary.Play2StartId},{summary.Play3StartId},{summary.Play4StartId}";
            return false;
        }

        if (summary.AfterRun.PossessionTeam != 0 || summary.AfterRun.Down != 2 || summary.AfterRun.YardsToGo != 5 || !summary.AfterRun.BallSpot.Equals(BallSpot.Own(35)) || summary.AfterRun.PlayNumber != summary.Play1StartId)
        {
            error = $"run checkpoint wrong {summary.AfterRun}";
            return false;
        }

        if (summary.AfterFirstDown.PossessionTeam != 0 || summary.AfterFirstDown.Down != 1 || summary.AfterFirstDown.YardsToGo != 10 || !summary.AfterFirstDown.BallSpot.Equals(BallSpot.Own(45)) || summary.AfterFirstDown.PlayNumber != summary.Play2StartId)
        {
            error = $"first-down checkpoint wrong {summary.AfterFirstDown}";
            return false;
        }

        if (summary.AfterTouchdown.Team0Score != 6 || summary.AfterTouchdown.Team1Score != 0 || summary.AfterTouchdown.PossessionTeam != 0 || summary.AfterTouchdown.Down != 1 || summary.AfterTouchdown.DriveId != 0 || summary.AfterTouchdown.WhistleReason != WhistleReason.Touchdown || summary.AfterTouchdown.PlayNumber != summary.Play3StartId)
        {
            error = $"touchdown checkpoint wrong {summary.AfterTouchdown}";
            return false;
        }

        if (summary.AfterTurnover.Team0Score != 6 || summary.AfterTurnover.PossessionTeam != 0 || summary.AfterTurnover.Down != 1 || summary.AfterTurnover.YardsToGo != 10 || summary.AfterTurnover.DriveId != 2 || summary.AfterTurnover.OffenseDirection != OffenseDirection.LeftToRight || !summary.AfterTurnover.BallSpot.Equals(BallSpot.Own(50)) || summary.AfterTurnover.WhistleReason != WhistleReason.Turnover || summary.AfterTurnover.PlayNumber != summary.Play4StartId)
        {
            error = $"turnover checkpoint wrong {summary.AfterTurnover}";
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
        int EndAbsoluteYard);

    private readonly record struct DriveLifecycleScenarioSummary(
        int Play1StartId,
        int Play2StartId,
        int Play3StartId,
        int Play4StartId,
        DriveCheckpoint AfterRun,
        DriveCheckpoint AfterFirstDown,
        DriveCheckpoint AfterTouchdown,
        DriveCheckpoint AfterTurnover);
}
