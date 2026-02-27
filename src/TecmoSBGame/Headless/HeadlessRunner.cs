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
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new CollisionContactSystem(events, loopState))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, match, play))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new PlayEndSystem(events, match, play, log: true))
            .AddSystem(new DownDistanceSystem(events, match, log: true))
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
        world.GetEntity(ballId).Get<BallStateComponent>().State = BallState.Held;
        world.GetEntity(ballId).Get<BallOwnerComponent>().OwnerEntityId = qbId;

        play.BallState = BallState.Held;
        play.BallOwnerEntityId = qbId;

        Console.WriteLine($"[headless] play: offense='{spawnedPlay.OffensivePlayName}' slot='{spawnedPlay.OffensiveSlot}' formation={spawnedPlay.OffensiveFormationId} playNo=0x{spawnedPlay.OffensivePlayNumber:X2}");
        Console.WriteLine($"[headless] play: defense='{spawnedPlay.DefensiveCallId}'");
        Console.WriteLine("[headless] assignments:");
        foreach (var a in spawnedPlay.Assignments.OrderBy(a => a.TeamIndex).ThenBy(a => a.IsOffense ? 0 : 1).ThenBy(a => a.EntityId))
        {
            Console.WriteLine($"  id={a.EntityId,4} team={a.TeamIndex} {(a.IsOffense ? "OFF" : "DEF")} role={a.Role,-3} slot={a.Slot,-5} :: {a.Summary}");
        }

        // Start in live play so the clock system can run during the headless slice.
        // (In the full game, this is driven by input + SnapResolutionSystem.)
        events.Publish(new SnapEvent(OffenseTeam: match.PossessionTeam, DefenseTeam: 1 - match.PossessionTeam));

        var elapsed = TimeSpan.FromSeconds(1.0 / 60.0);
        var total = TimeSpan.Zero;

        for (var i = 0; i < ticks; i++)
        {
            total += elapsed;
            events.BeginTick();
            world.Update(new GameTime(total, elapsed));
        }

        Console.WriteLine($"[headless] completed ticks={ticks}");
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
