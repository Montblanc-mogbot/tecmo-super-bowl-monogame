using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Factories;
using TecmoSBGame.Spawning;
using TecmoSBGame.State;
using TecmoSBGame.Systems;

namespace TecmoSBGame.Headless;

/// <summary>
/// Scaffold runner that compares a headless simulation against a recorded NES trace.
///
/// Usage (planned):
///   dotnet run --project src/TecmoSBGame -- headless-nes-compare <trace.json>
///
/// This is intentionally minimal until we have an actual capture pipeline.
/// </summary>
public static class NesTraceCompareRunner
{
    public static int Run(string tracePath, int maxTicks = 180)
    {
        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"[nes-compare] trace not found: {tracePath}");
            return 2;
        }

        var traceJson = File.ReadAllText(tracePath);
        var trace = JsonSerializer.Deserialize<NesTrace>(traceJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (trace is null || trace.Frames.Count == 0)
        {
            Console.Error.WriteLine("[nes-compare] invalid/empty trace");
            return 2;
        }

        var match = new MatchState();
        var play = new PlayState();
        var events = new TecmoSBGame.Events.GameEvents();

        var formationData = FormationDataYamlLoader.LoadFromFile(Path.Combine("content", "formations", "formation_data.yaml"));
        var playList = PlayListYamlLoader.LoadFromFile(Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = DefensePlayYamlLoader.LoadFromFile(Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));

        var gameLoopConfig = TecmoSB.GameLoopYamlLoader.LoadFromFile(Path.Combine("content", "gameloop", "bank17_18_main_game_loop.yaml"));
        var onFieldLoopConfig = TecmoSB.OnFieldLoopYamlLoader.LoadFromFile(Path.Combine("content", "onfieldloop", "bank19_20_on_field_gameplay_loop.yaml"));
        var loopState = new LoopState(new TecmoSB.GameLoopMachine(gameLoopConfig), new TecmoSB.OnFieldLoopMachine(onFieldLoopConfig));

        // Core headless stack (re-use the same ordering as HeadlessRunner).
        var world = new MonoGame.Extended.Entities.WorldBuilder()
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(new SnapResolutionSystem(events, match, play))
            .AddSystem(new QbDropbackSystem(events, match, play))
            .AddSystem(new ReadProgressionSystem(events, match, play))
            .AddSystem(new PassFlightStartSystem(events, play))
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new PassFlightCompleteSystem(events, play))
            .AddSystem(new BlockerAISystem(events, loopState, play))
            .AddSystem(new CollisionContactSystem(events, loopState, play))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new LoopMachineSystem(loopState, events))
            .Build();

        var formationSpawner = new FormationSpawner();
        var playSpawner = new PlaySpawner();

        var chosenOffPlay = playList.PlayList.First();
        var formationId = formationData.OffensiveFormations.Any(f => f.Id == chosenOffPlay.Formation)
            ? chosenOffPlay.Formation
            : "00";

        var offense = formationSpawner.Spawn(world, formationData, formationId, teamIndex: 0, isOffense: true, playerControlled: false);
        var defenseEntityIds = SpawnPlaceholderDefense(world, teamIndex: 1);

        var spawned = playSpawner.Spawn(
            world,
            playList,
            defensePlays,
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseEntityIds);

        match.PossessionTeam = 0;
        match.OffenseDirection = OffenseDirection.LeftToRight;
        match.BallSpot = BallSpot.Own(25);

        play.ResetForNewPlay(trace.Meta.PlayId != 0 ? trace.Meta.PlayId : 1, trace.Meta.StartAbsoluteYard != 0 ? trace.Meta.StartAbsoluteYard : PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection));
        play.Phase = PlayPhase.InPlay;

        // Give ball to QB.
        var qbId = offense.Players.First(p => p.Role == PlayerRole.QB).EntityId;
        world.GetEntity(qbId).Get<BallCarrierComponent>().HasBall = true;

        Console.WriteLine($"[nes-compare] running ticks={Math.Min(maxTicks, trace.Frames.Count)} trace='{Path.GetFileName(tracePath)}'");

        var elapsed = TimeSpan.FromSeconds(1.0 / 60.0);
        var total = TimeSpan.Zero;

        var ticks = Math.Min(maxTicks, trace.Frames.Count);
        var failures = 0;

        for (var i = 0; i < ticks; i++)
        {
            total += elapsed;
            events.BeginTick();

            if (i == 0)
                events.Publish(new TecmoSBGame.Events.SnapEvent(OffenseTeam: match.PossessionTeam, DefenseTeam: 1 - match.PossessionTeam));

            world.Update(new GameTime(total, elapsed));

            // Compare QB position if present.
            var frame = trace.Frames[i];
            if (frame.Positions.TryGetValue("QB", out var qb))
            {
                var p = world.GetEntity(qbId).Get<PositionComponent>().Position;
                var dx = MathF.Abs(p.X - qb.X);
                var dy = MathF.Abs(p.Y - qb.Y);

                if (dx > 2.0f || dy > 2.0f)
                {
                    failures++;
                    if (failures <= 10)
                        Console.WriteLine($"[nes-compare] FAIL t={i} QB sim=({p.X:0.0},{p.Y:0.0}) nes=({qb.X:0.0},{qb.Y:0.0}) d=({dx:0.0},{dy:0.0})");
                }
            }
        }

        Console.WriteLine($"[nes-compare] done failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static List<int> SpawnPlaceholderDefense(MonoGame.Extended.Entities.World world, int teamIndex)
    {
        var ids = new List<int>(capacity: 11);

        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 76), PlayerRole.DL, slot: "RE"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 100), PlayerRole.DL, slot: "DT"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 124), PlayerRole.DL, slot: "NT"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(170, 148), PlayerRole.DL, slot: "LE"));

        ids.Add(SpawnDefender(world, teamIndex, new Vector2(190, 92), PlayerRole.LB, slot: "ROLB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(192, 112), PlayerRole.LB, slot: "MLB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(190, 132), PlayerRole.LB, slot: "LOLB"));

        ids.Add(SpawnDefender(world, teamIndex, new Vector2(210, 70), PlayerRole.DB, slot: "RCB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(210, 154), PlayerRole.DB, slot: "LCB"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(222, 104), PlayerRole.DB, slot: "FS"));
        ids.Add(SpawnDefender(world, teamIndex, new Vector2(222, 120), PlayerRole.DB, slot: "SS"));

        return ids;
    }

    private static int SpawnDefender(MonoGame.Extended.Entities.World world, int teamIndex, Vector2 pos, PlayerRole role, string slot)
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
