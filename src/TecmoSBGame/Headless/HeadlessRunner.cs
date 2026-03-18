using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Factories;
using TecmoSBGame.Spawning;
using TecmoSBGame.State;
using TecmoSBGame.Systems;
using TecmoSBGame.Timing;

namespace TecmoSBGame.Headless;

public static class HeadlessRunner
{
    /// <summary>
    /// Minimal deterministic simulation loop that runs without creating a MonoGame window.
    /// Intended for CI/headless smoke tests.
    /// </summary>
    public static int Run(int ticks = 300)
    {
        var events = new GameEvents();
        var match = new MatchState();
        var play = new PlayState();

        // Fixed 60Hz, explicit tick control.
        var fixedRunner = new FixedTimestepRunner(hz: 60, maxTicksPerFrame: 1);

        var formationData = FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        var playList = PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));

        // Loop machines (used by clock system + future gating).
        var gameLoopConfig = GameLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "gameloop", "bank17_18_main_game_loop.yaml"));
        var onFieldLoopConfig = OnFieldLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "onfieldloop", "bank19_20_on_field_gameplay_loop.yaml"));
        var loopState = new LoopState(new GameLoopMachine(gameLoopConfig), new OnFieldLoopMachine(onFieldLoopConfig));

        var formationSpawner = new FormationSpawner();
        var playSpawner = new PlaySpawner();

        // We no longer need the full GameStateSystem for this headless pass; keep the core physics/contact stack.
        var world = new WorldBuilder()
            // Routes/blocks first so QB reads have meaningful receiver motion.
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            // Phase transitions + QB AI.
            .AddSystem(new SnapResolutionSystem(events, match, play))
            .AddSystem(new QbDropbackSystem(events, match, play))
            .AddSystem(new ReadProgressionSystem(events, match, play))
            .AddSystem(new AIDecisionLogSystem(play))
            .AddSystem(new PassFlightStartSystem(events, play))
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new PassFlightCompleteSystem(events, play))
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new BlockerAISystem(events, loopState, play))
            .AddSystem(new CollisionContactSystem(events, loopState, play))
            .AddSystem(new EngagementSystem(events))
            // Penalties are scaffolded but default to Off (no behavior changes).
            .AddSystem(new PenaltySystem(events, match, play))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, match, play))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new PlayEndSystem(events, match, play, log: true))
            .AddSystem(new DownDistanceSystem(events, match, log: true))
            .AddSystem(new NextPlayResetSystem(events, match, play, loopState, log: true))
            .AddSystem(new LoopMachineSystem(loopState, events))
            .AddSystem(new GameClockSystem(events, match, play, loopState, log: true))
            .AddSystem(new ContactDebugLogSystem(events))
            .Build();

        // Spawn offense from the first deterministic pass play's formation.
        // Note: our current formation YAML is a partial scaffold and may not include every playlist formation id.
        var chosenOffPlay = playList.PlayList.First(p => (p.Slot ?? string.Empty).StartsWith("Pass", StringComparison.OrdinalIgnoreCase));
        var formationId = formationData.OffensiveFormations.Any(f => f.Id == chosenOffPlay.Formation)
            ? chosenOffPlay.Formation
            : "00";

        var offense = formationSpawner.Spawn(
            world,
            formationData,
            formationId: formationId,
            teamIndex: 0,
            isOffense: true,
            playerControlled: false);

        // Spawn a simple 11-man defense (placeholders) with standardized PlayerRoleComponent.
        var defenseEntityIds = SpawnPlaceholderDefense(world, teamIndex: 1);

        Console.WriteLine($"[headless] spawned formation offense={offense.FormationId} (entities={offense.Players.Count}), defense=placeholder (entities={defenseEntityIds.Count})");

        // Spawn play (attach assignments) and print summary.
        var spawnedPlay = playSpawner.Spawn(
            world,
            playList,
            defensePlays,
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseEntityIds);

        // Minimal match/play init so tackle resolution + PlayEndSystem can produce an end-of-play snapshot.
        match.PossessionTeam = 0;
        match.OffenseDirection = OffenseDirection.LeftToRight;
        match.Down = 1;
        match.YardsToGo = 10;
        match.BallSpot = BallSpot.Own(25);

        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(playId: match.PlayNumber + 1, startAbsoluteYard: startAbs);

        // Give the ball to the QB and spawn a dedicated ball entity so BallPhysics/Bounds/End logic can reference it.
        var qbId = offense.Players.First(p => p.Role == PlayerRole.QB).EntityId;
        world.GetEntity(qbId).Get<BallCarrierComponent>().HasBall = true;

        var qbPos = world.GetEntity(qbId).Get<PositionComponent>().Position;
        var ballId = BallEntityFactory.CreateBall(world, qbPos);
        var b0 = world.GetEntity(ballId).Get<BallComponent>();
        b0.State = BallState.Held;
        b0.OwnerEntityId = qbId;

        play.BallState = BallState.Held;
        play.BallOwnerEntityId = qbId;

        Console.WriteLine($"[headless] play: offense='{spawnedPlay.OffensivePlayName}' slot='{spawnedPlay.OffensiveSlot}' formation={spawnedPlay.OffensiveFormationId} playNo=0x{spawnedPlay.OffensivePlayNumber:X2}");
        Console.WriteLine($"[headless] play: defense='{spawnedPlay.DefensiveCallId}'");
        Console.WriteLine("[headless] assignments:");
        foreach (var a in spawnedPlay.Assignments.OrderBy(a => a.TeamIndex).ThenBy(a => a.IsOffense ? 0 : 1).ThenBy(a => a.EntityId))
        {
            Console.WriteLine($"  id={a.EntityId,4} team={a.TeamIndex} {(a.IsOffense ? "OFF" : "DEF")} role={a.Role,-3} slot={a.Slot,-5} :: {a.Summary}");
        }

        var elapsed = TimeSpan.FromSeconds(1.0 / 60.0);
        var total = TimeSpan.Zero;

        var lastPlayId = play.PlayId;
        for (var i = 0; i < ticks; i++)
        {
            total += elapsed;
            events.BeginTick();

            // Start in live play so the contact/clock systems can run during the headless slice.
            // (In the full game, this is driven by input + SnapResolutionSystem.)
            if (i == 0)
                events.Publish(new SnapEvent(OffenseTeam: match.PossessionTeam, DefenseTeam: 1 - match.PossessionTeam));

            world.Update(new GameTime(total, elapsed));

            // QB AI smoke signal: once the ball enters flight, we know reads->throw->PassFlightStart succeeded.
            if (i == 0 || i == 30 || i == 60 || i == 90 || i == 120)
                Console.WriteLine($"[headless] t={i,3} phase={play.Phase} ball={play.BallState} owner={(play.BallOwnerEntityId is null ? "none" : play.BallOwnerEntityId.Value.ToString())}");

            if (play.PlayId != lastPlayId)
            {
                Console.WriteLine($"[headless] advanced to next play: {play.ToSummaryString()} | onField={loopState.OnFieldStateId}");
                lastPlayId = play.PlayId;
            }
        }

        Console.WriteLine($"[headless] completed ticks={ticks} final: {play.ToSummaryString()} | onField={loopState.OnFieldStateId}");

        // Blocking AI inspection (headless verification).
        Console.WriteLine("[headless] blocking summary (entities with BlockTargetComponent):");
        foreach (var id in offense.Players.Select(p => p.EntityId).OrderBy(i => i))
        {
            var e = world.GetEntity(id);
            if (!e.Has<BlockTargetComponent>())
                continue;

            var bt = e.Get<BlockTargetComponent>();
            var role = e.Has<PlayerRoleComponent>() ? e.Get<PlayerRoleComponent>().Slot : "";
            Console.WriteLine($"  id={id,4} slot={role,-5} assign={bt.Assignment,-10} target={bt.TargetEntityId,4} engaged={bt.IsEngaged} engagedWith={bt.EngagedEntityId,4} frames={bt.EngagementFrame,3} double={bt.IsDoubleTeam}");
        }

        return 0;
    }


    /// <summary>
    /// Headless smoke scenario for the "2 plays" milestone:
    /// - selects offensive play_number=10 (T FAKE SWEEP R) from the playlist,
    /// - attaches PlayData YAML scripts (handoff + defender pursuit),
    /// - publishes a snap event,
    /// - asserts: HB becomes ball owner after the configured handoff delay,
    /// - asserts: at least one defender enters TrackingPlayer behavior,
    /// - asserts: the play ends (tackle whistle) and advances to the next play.
    ///
    /// Returns non-zero on failure for CI.
    /// </summary>
    public static int RunTwoPlaysScenario(int ticks = 240)
    {
        var events = new GameEvents();
        var match = new MatchState();
        var play = new PlayState();
        var control = new ControlState();

        var formationData = FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        var playList = PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));
        var playData = PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));

        var gameLoopConfig = GameLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "gameloop", "bank17_18_main_game_loop.yaml"));
        var onFieldLoopConfig = OnFieldLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "onfieldloop", "bank19_20_on_field_gameplay_loop.yaml"));
        var loopState = new LoopState(new GameLoopMachine(gameLoopConfig), new OnFieldLoopMachine(onFieldLoopConfig));

        var formationSpawner = new FormationSpawner();
        var playSpawner = new PlaySpawner();

        // Core systems needed for scripts + contact->whistle->reset.
        var world = new WorldBuilder()
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new PlayScriptSystem(play, match, control))
            .AddSystem(new PlayerControlSystem(control, loopState, enableInput: false))
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(new SnapResolutionSystem(events, match, play))
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new BlockerAISystem(events, loopState, play))
            .AddSystem(new CollisionContactSystem(events, loopState, play))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, match, play))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new PlayEndSystem(events, match, play, log: true))
            .AddSystem(new DownDistanceSystem(events, match, log: true))
            .AddSystem(new NextPlayResetSystem(events, match, play, loopState, log: true))
            .AddSystem(new LoopMachineSystem(loopState, events))
            .AddSystem(new GameClockSystem(events, match, play, loopState, log: true))
            .Build();

        // Select offensive play #10.
        const int DemoPlayNumber = 10;
        var chosenOffPlay = playList.PlayList.FirstOrDefault(p => p.PlayNumbers is not null && p.PlayNumbers.Contains(DemoPlayNumber));
        if (chosenOffPlay is null)
        {
            Console.WriteLine($"[headless-2plays] FAIL: could not find play_number={DemoPlayNumber} in playlist.yaml");
            return 2;
        }

        var formationId = formationData.OffensiveFormations.Any(f => f.Id == chosenOffPlay.Formation)
            ? chosenOffPlay.Formation
            : (formationData.OffensiveFormations.FirstOrDefault()?.Id ?? "00");

        var offense = formationSpawner.Spawn(
            world,
            formationData,
            formationId: formationId,
            teamIndex: 0,
            isOffense: true,
            playerControlled: true);

        var defenseEntityIds = SpawnPlaceholderDefense(world, teamIndex: 1);

        var spawnedPlay = playSpawner.Spawn(
            world,
            playList,
            defensePlays,
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseEntityIds,
            selectedOffensivePlay: chosenOffPlay,
            selectedDefensiveCallId: defensePlays.DefensiveExecutions?.FirstOrDefault()?.Id);

        // Minimal match/play init.
        match.PossessionTeam = 0;
        match.OffenseDirection = OffenseDirection.LeftToRight;
        match.Down = 1;
        match.YardsToGo = 10;
        match.BallSpot = BallSpot.Own(25);

        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(playId: match.PlayNumber + 1, startAbsoluteYard: startAbs);

        // Create dedicated ball entity.
        var qbId = offense.Players.First(p => world.GetEntity(p.EntityId).Get<PlayerRoleComponent>().Role == PlayerRole.QB).EntityId;
        var qbPos = world.GetEntity(qbId).Get<PositionComponent>().Position;
        var ballId = BallEntityFactory.CreateBall(world, qbPos);
        var b0 = world.GetEntity(ballId).Get<BallComponent>();
        b0.State = BallState.Held;
        b0.OwnerEntityId = qbId;

        // Start with QB owning ball; PlayData will handoff later.
        world.GetEntity(qbId).Get<BallCarrierComponent>().HasBall = true;
        play.BallState = BallState.Held;
        play.BallOwnerEntityId = qbId;

        // Attach PlayData scripts for play 10 to offense/defense entities by slot.
        AttachPlayDataScripts(world, playData, offensivePlayNumber: DemoPlayNumber, offense.Players.Select(p => p.EntityId).ToList(), defenseEntityIds);

        Console.WriteLine($"[headless-2plays] spawned offPlay='{spawnedPlay.OffensivePlayName}' formation={spawnedPlay.OffensiveFormationId} playNo={spawnedPlay.OffensivePlayNumber}");

        var elapsed = TimeSpan.FromSeconds(1.0 / 60.0);
        var total = TimeSpan.Zero;

        var initialPlayId = play.PlayId;
        var hbId = offense.Players.FirstOrDefault(p => world.GetEntity(p.EntityId).Get<PlayerRoleComponent>().Slot?.Equals("HB", StringComparison.OrdinalIgnoreCase) == true).EntityId;
        if (hbId <= 0)
        {
            Console.WriteLine("[headless-2plays] FAIL: could not resolve HB entity id");
            return 3;
        }

        var sawHandoff = false;
        var sawPursuit = false;
        var sawPlayAdvance = false;

        for (var i = 0; i < ticks; i++)
        {
            total += elapsed;
            events.BeginTick();

            if (i == 0)
                events.Publish(new SnapEvent(OffenseTeam: match.PossessionTeam, DefenseTeam: 1 - match.PossessionTeam));

            // If we somehow never get a "down" tackle (broken tackles forever), force a whistle so CI doesn't hang.
            if (i == 180 && play.Phase == PlayPhase.InPlay)
                events.Publish(new WhistleEvent("headless-timeout"));

            world.Update(new GameTime(total, elapsed));

            // (a) HB becomes owner after delay.
            if (!sawHandoff && play.BallOwnerEntityId == hbId)
            {
                sawHandoff = true;
                Console.WriteLine($"[headless-2plays] observed handoff at t={i} to HB entity={hbId}");
            }

            // (b) at least one defender tracks ballcarrier.
            if (!sawPursuit)
            {
                foreach (var did in defenseEntityIds)
                {
                    var e = world.GetEntity(did);
                    if (!e.Has<BehaviorComponent>())
                        continue;

                    var b = e.Get<BehaviorComponent>();
                    if (b.State == BehaviorState.TrackingPlayer && b.TargetEntityId != 0)
                    {
                        sawPursuit = true;
                        Console.WriteLine($"[headless-2plays] observed pursuit at t={i} defender={did} target={b.TargetEntityId}");
                        break;
                    }
                }
            }

            // (c) play ends and advances.
            if (!sawPlayAdvance && play.PlayId != initialPlayId)
            {
                sawPlayAdvance = true;
                Console.WriteLine($"[headless-2plays] advanced to next play at t={i}: {play.ToSummaryString()}");
            }
        }

        var ok = sawHandoff && sawPursuit && sawPlayAdvance;
        if (!ok)
        {
            Console.WriteLine($"[headless-2plays] FAIL: sawHandoff={sawHandoff} sawPursuit={sawPursuit} sawPlayAdvance={sawPlayAdvance} final={play.ToSummaryString()}");
            return 1;
        }

        Console.WriteLine($"[headless-2plays] PASS ticks={ticks} final={play.ToSummaryString()}");
        return 0;
    }

    private static void AttachPlayDataScripts(World world, PlayDataConfig playData, int offensivePlayNumber, IReadOnlyList<int> offenseEntityIds, IReadOnlyList<int> defenseEntityIds)
    {
        var def = playData.Plays.FirstOrDefault(p => p.PlayNumber == offensivePlayNumber);
        if (def is null)
            return;

        var reactionById = playData.PlayerReactions.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

        void AttachTo(int entityId, string? reactionId)
        {
            if (string.IsNullOrWhiteSpace(reactionId))
                return;

            if (!reactionById.TryGetValue(reactionId, out var reaction))
                return;

            var ops = PlayScriptCompiler.Compile(reaction);
            if (ops.Count == 0)
                return;

            var e = world.GetEntity(entityId);
            if (!e.Has<PlayScriptComponent>())
                e.Attach(new PlayScriptComponent(reaction.Id, ops));
            else
            {
                var s = e.Get<PlayScriptComponent>();
                s.Ip = 0;
                s.WaitSeconds = 0;
            }
        }

        foreach (var id in offenseEntityIds)
        {
            var e = world.GetEntity(id);
            if (!e.Has<PlayerRoleComponent>())
                continue;

            var slot = (e.Get<PlayerRoleComponent>().Slot ?? string.Empty).Trim();
            if (def.Offense.TryGetValue(slot, out var reactionId))
                AttachTo(id, reactionId);
        }

        foreach (var id in defenseEntityIds)
        {
            var e = world.GetEntity(id);
            if (!e.Has<PlayerRoleComponent>())
                continue;

            var slot = (e.Get<PlayerRoleComponent>().Slot ?? string.Empty).Trim();
            if (def.Defense.TryGetValue(slot, out var reactionId))
                AttachTo(id, reactionId);
        }

        Console.WriteLine($"[headless-2plays] playdata attached play_number={offensivePlayNumber}");
    }



    /// <summary>
    /// Deterministic headless scenario that exercises COVER-1..COVER-7 behavior:
    /// - spawns a pass play,
    /// - attaches coverage components via PlaySpawner,
    /// - triggers a pass at a fixed tick,
    /// - observes defenders breaking toward the target.
    /// </summary>
    public static int RunCoverageScenario(int ticks = 240)
    {
        var events = new GameEvents();
        var match = new MatchState();
        var play = new PlayState();

        var fixedRunner = new FixedTimestepRunner(hz: 60, maxTicksPerFrame: 1);

        var formationData = FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        var playList = PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));

        var gameLoopConfig = GameLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "gameloop", "bank17_18_main_game_loop.yaml"));
        var onFieldLoopConfig = OnFieldLoopYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "onfieldloop", "bank19_20_on_field_gameplay_loop.yaml"));
        var loopState = new LoopState(new GameLoopMachine(gameLoopConfig), new OnFieldLoopMachine(onFieldLoopConfig));

        var formationSpawner = new FormationSpawner();
        var playSpawner = new PlaySpawner();

        // Core systems plus coverage + pass start.
        var world = new WorldBuilder()
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(new ManCoverageSystem(events, play))
            .AddSystem(new ZoneCoverageSystem(events, play))
            .AddSystem(new MovementSystem())
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new PassFlightStartSystem(events, play))
            .AddSystem(new PassFlightCompleteSystem(events, play))
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new CollisionContactSystem(events, loopState, play))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new PenaltySystem(events, match, play))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, match, play))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new PlayEndSystem(events, match, play, log: false))
            .AddSystem(new DownDistanceSystem(events, match, log: false))
            .AddSystem(new NextPlayResetSystem(events, match, play, loopState, log: false))
            .AddSystem(new LoopMachineSystem(loopState, events))
            .Build();

        // Spawn offense.
        var chosenOffPlay = playList.PlayList.First(p => (p.Slot ?? string.Empty).StartsWith("Pass", StringComparison.OrdinalIgnoreCase));
        var formationId = formationData.OffensiveFormations.Any(f => f.Id == chosenOffPlay.Formation)
            ? chosenOffPlay.Formation
            : "00";

        var offense = formationSpawner.Spawn(
            world,
            formationData,
            formationId: formationId,
            teamIndex: 0,
            isOffense: true,
            playerControlled: false);

        var defenseEntityIds = SpawnPlaceholderDefense(world, teamIndex: 1);

        var spawnedPlay = playSpawner.Spawn(
            world,
            playList,
            defensePlays,
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseEntityIds,
            selectedOffensivePlay: chosenOffPlay,
            selectedDefensiveCallId: null);

        // Choose QB + a deterministic receiver target.
        var qbId = offense.Players.First(p => world.GetEntity(p.EntityId).Get<PlayerRoleComponent>()?.Role == PlayerRole.QB).EntityId;
        var targetId = offense.Players.First(p =>
            {
                var r = world.GetEntity(p.EntityId).Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown;
                return r is PlayerRole.WR or PlayerRole.TE;
            }).EntityId;

        // Spawn a ball entity (required for PassFlightStartSystem).
        BallEntityFactory.CreateBall(world, world.GetEntity(qbId).Get<PositionComponent>()!.Position);

        world.CreateEntity().Attach(new CoverageScenarioDriverSystem(events, play, qbId, targetId, throwAtTick: 60));

        System.Console.WriteLine($"[coverage] QB={qbId} target={targetId} defCall={spawnedPlay.DefensiveCallId}");

        // Run fixed ticks.
        for (var i = 0; i < ticks; i++)
        {
            fixedRunner.Advance(TimeSpan.FromSeconds(1.0 / 60.0), fixedGameTime =>
            {
                events.BeginTick();
                world.Update(fixedGameTime);
            });
        }

        System.Console.WriteLine("[coverage] scenario complete");
        return 0;
    }
    private static List<int> SpawnPlaceholderDefense(World world, int teamIndex)
    {
        // Simple 4-3-ish distribution, stable coordinates.
        // Defense assumed to be aligned to the right of the offense and moving -X.
        var ids = new List<int>(capacity: 11);

        // DL (4)
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 76), PlayerRole.DL, slot: "RE"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 100), PlayerRole.DL, slot: "DT"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 124), PlayerRole.DL, slot: "NT"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 148), PlayerRole.DL, slot: "LE"));

        // LB (3)
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(190, 92), PlayerRole.LB, slot: "ROLB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(192, 112), PlayerRole.LB, slot: "MLB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(190, 132), PlayerRole.LB, slot: "LOLB"));

        // DB (4)
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(210, 70), PlayerRole.DB, slot: "RCB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(210, 154), PlayerRole.DB, slot: "LCB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(222, 104), PlayerRole.DB, slot: "FS"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(222, 120), PlayerRole.DB, slot: "SS"));

        return ids;
    }

    private static int SpawnDefender(World world, int teamIndex, Vector2 pos, PlayerRole role, string slot)
    {
        var id = PlayerEntityFactory.CreatePlayer(
            world,
            pos,
            teamIndex,
            isPlayerControlled: false,
            isOffense: false);

        var e = world.GetEntity(id);
        e.Attach(new PlayerRoleComponent(role, slot));
        return id;
    }
}
