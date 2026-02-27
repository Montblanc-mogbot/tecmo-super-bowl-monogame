using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;
using TecmoSBGame.Timing;
using TecmoSBGame.Spawning;
using TecmoSBGame.Factories;

namespace TecmoSBHeadless;

internal static class Program
{
    // (Fixed-step utilities live in TecmoSBGame.Timing)

    public static int Main(string[] args)
    {
        var ticks = GetIntArg(args, "--ticks", 600);
        var hz = GetIntArg(args, "--hz", 60);
        var scenarioName = (GetStringArg(args, "--scenario", null) ?? "kickoff").Trim().ToLowerInvariant();
        var yamlRoot = GetStringArg(args, "--content", null) ?? FindYamlRoot();

        Console.WriteLine($"[Headless] YAML root: {yamlRoot}");
        var yaml = LoadYamlContent(yamlRoot);

        var events = new GameEvents();
        var matchState = new MatchState();
        var playState = new PlayState();
        var gameState = new GameStateSystem(matchState, playState, events, headlessAutoAdvance: true);

        var loopState = new LoopState(new GameLoopMachine(yaml.GameLoop), new OnFieldLoopMachine(yaml.OnFieldLoop));
        var controlState = new ControlState();

        var builder = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(new PreSnapSystem(loopState, matchState, playState))
            .AddSystem(new PreSnapBallPlacementSystem(loopState, matchState, playState))
            .AddSystem(new PlayerControlSystem(controlState, loopState, enableInput: false))
            .AddSystem(new ActionResolutionSystem(events, matchState, playState))
            .AddSystem(new SnapResolutionSystem(events, matchState, playState))
            .AddSystem(new CollisionContactSystem(events, loopState))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, matchState, playState))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new PassFlightStartSystem(events, playState));

        // Kickoff slice is driven by GameStateSystem. For non-kickoff scenarios, keep it out of the stack.
        if (scenarioName == "kickoff")
            builder.AddSystem(gameState);

        var world = builder
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new PassFlightCompleteSystem(events, playState))
            .AddSystem(new BallBoundsSystem(events, matchState, playState))
            // TEMP: fumbles triggered off tackle whistle until tackle rules resolve.
            .AddSystem(new FumbleOnTackleWhistleSystem(events, playState))
            .AddSystem(new FumbleResolutionSystem(events, playState))
            .AddSystem(new LooseBallPickupSystem(events, playState))
            .AddSystem(new LoopMachineSystem(loopState, events))
            .AddSystem(new ContactDebugLogSystem(events))
            .AddSystem(new TecmoSBGame.Headless.HeadlessContactSeederSystem())
            .Build();

        // Scenario selection.
        // - kickoff: existing slice driven by GameStateSystem
        // - presnap: scrimmage pre-snap placement + snap transition
        var kickoffScenario = default(GameStateSystem.KickoffScenarioIds);
        ScrimmageScenario? scrimmageScenario = null;

        if (scenarioName == "kickoff")
        {
            kickoffScenario = gameState.SpawnKickoffScenario(world);
        }
        else if (scenarioName == "presnap")
        {
            scrimmageScenario = SpawnScrimmagePreSnapScenario(world, yamlRoot, matchState, playState);
        }
        else
        {
            Console.WriteLine($"[Headless] Unknown scenario '{scenarioName}'. Use --scenario kickoff|presnap");
            return 2;
        }

        var fixedStep = new FixedTimestepRunner(hz, maxTicksPerFrame: int.MaxValue, maxAccumulated: TimeSpan.FromDays(1));
        for (var i = 0; i < ticks; i++)
        {
            var prevWhistle = playState.WhistleReason;

            // Scenario hook: trigger a snap deterministically after a few pre-snap ticks.
            if (scenarioName == "presnap" && scrimmageScenario is not null && i == 10)
            {
                var qb = world.GetEntity(scrimmageScenario.QbId);
                if (qb.Has<PlayerActionStateComponent>())
                    qb.Get<PlayerActionStateComponent>().PendingCommand = PlayerActionCommand.Snap;
            }

            events.BeginTick();
            fixedStep.TickOnce(world.Update);

            // Detect whistle reasons that are typically produced by BallBoundsSystem.
            if (scenarioName == "kickoff" && prevWhistle == WhistleReason.None && playState.WhistleReason is WhistleReason.OutOfBounds or WhistleReason.Touchback or WhistleReason.Safety)
            {
                var ball = world.GetEntity(kickoffScenario.BallId);
                var p = ball.Get<PositionComponent>().Position;
                Console.WriteLine($"  [bounds] whistle={playState.WhistleReason} ball=({p.X:0.0},{p.Y:0.0})");
            }

            // Print pass outcomes when they occur.
            events.Drain<PassResolvedEvent>(e =>
            {
                var target = e.TargetId is null ? "none" : e.TargetId.Value.ToString(CultureInfo.InvariantCulture);
                var winner = e.WinnerId is null ? "none" : e.WinnerId.Value.ToString(CultureInfo.InvariantCulture);
                Console.WriteLine($"  [pass] outcome={e.Outcome} passer={e.PasserId} target={target} winner={winner} ball=({e.BallPosition.X:0.0},{e.BallPosition.Y:0.0})");
            });

            // Print fumbles and loose-ball pickups.
            events.Drain<FumbleEvent>(e =>
            {
                Console.WriteLine($"  [fumble] carrier={e.CarrierId} cause={e.Cause}");
            });
            events.Drain<LooseBallPickupEvent>(e =>
            {
                Console.WriteLine($"  [pickup] picker={e.PickerId} ball=({e.BallPosition.X:0.0},{e.BallPosition.Y:0.0})");
            });

            // Snapshot at start, once per second, and at end.
            if (i == 0 || (i + 1) % hz == 0 || i == ticks - 1)
            {
                if (scenarioName == "kickoff")
                    PrintKickoffSummary(world, gameState, loopState, controlState, i + 1, hz, kickoffScenario);
                else if (scrimmageScenario is not null)
                    PrintScrimmageSummary(world, loopState, i + 1, hz, matchState, playState, scrimmageScenario);

            }
        }

        return 0;
    }

    private sealed record LoadedYaml(SimConfig Sim, GameLoopConfig GameLoop, OnFieldLoopConfig OnFieldLoop);

    private static LoadedYaml LoadYamlContent(string yamlRoot)
    {
        // Minimal proof that we can load the same YAML the game uses.
        var sim = SimConfigYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "sim/config.yaml"));
        var loop = GameLoopYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "gameloop/bank17_18_main_game_loop.yaml"));
        var onField = OnFieldLoopYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "onfieldloop/bank19_20_on_field_gameplay_loop.yaml"));

        Console.WriteLine($"[Headless] Loaded YAML: sim.maxScoreLimit={sim.MaxScoreLimit} yardsForFD={sim.YardsForFirstDown}");
        Console.WriteLine($"[Headless] Loaded YAML: gameLoop.states={loop.States.Count}");
        Console.WriteLine($"[Headless] Loaded YAML: onFieldLoop.states={onField.States.Count}");

        return new LoadedYaml(sim, loop, onField);
    }

    private static void PrintKickoffSummary(World world, GameStateSystem gameState, LoopState loopState, ControlState controlState, int tick, int hz, GameStateSystem.KickoffScenarioIds scenario)
    {
        var t = (tick / (double)hz).ToString("0.000", CultureInfo.InvariantCulture);
        Console.WriteLine($"[t={t}s tick={tick}] phase={gameState.CurrentPhase} phaseTimer={gameState.PhaseTimer:0.000}");
        Console.WriteLine($"  loops: game={loopState.GameLoopStateId} onField={loopState.OnFieldStateId} (onFieldTimer={loopState.OnFieldSecondsInState:0.000}s)");
        Console.WriteLine($"  match: {gameState.MatchState.ToSummaryString()}");
        Console.WriteLine($"  play:  {gameState.PlayState.ToSummaryString()}");

        PrintControlled(world, controlState);

        PrintEntity(world, "kicker", scenario.KickerId);
        PrintEntity(world, "returner", scenario.ReturnerId);
        PrintBall(world, scenario.BallId);

        Console.WriteLine();
    }

    private sealed record ScrimmageScenario(int QbId, int CenterId, int BallId);

    private static void PrintScrimmageSummary(World world, LoopState loopState, int tick, int hz, MatchState matchState, PlayState playState, ScrimmageScenario scenario)
    {
        var t = (tick / (double)hz).ToString("0.000", CultureInfo.InvariantCulture);
        Console.WriteLine($"[t={t}s tick={tick}] scenario=presnap");
        Console.WriteLine($"  loops: game={loopState.GameLoopStateId} onField={loopState.OnFieldStateId} (onFieldTimer={loopState.OnFieldSecondsInState:0.000}s)");
        Console.WriteLine($"  match: {matchState.ToSummaryString()}");
        Console.WriteLine($"  play:  {playState.ToSummaryString()}");

        PrintEntity(world, "QB", scenario.QbId);
        PrintEntity(world, "C", scenario.CenterId);
        PrintBall(world, scenario.BallId);

        Console.WriteLine();
    }

    private static ScrimmageScenario SpawnScrimmagePreSnapScenario(World world, string yamlRoot, MatchState match, PlayState play)
    {
        // Minimal deterministic scrimmage setup (no kickoff slice).
        match.PossessionTeam = 0;
        match.OffenseDirection = OffenseDirection.LeftToRight;
        match.Down = 1;
        match.YardsToGo = 10;
        match.BallSpot = BallSpot.Own(25);

        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        play.ResetForNewPlay(match.PlayNumber + 1, startAbs);

        // Spawn a dedicated ball entity; pre-snap systems will pin it to the LOS.
        var ballId = BallEntityFactory.CreateBall(world, new Vector2(40, 112));

        // Spawn offense from YAML formations.
        var formationData = FormationDataYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "formations", "formation_data.yaml"));
        var playList = PlayListYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "playcall", "playlist.yaml"));
        var defensePlays = DefensePlayYamlLoader.LoadFromFile(Path.Combine(yamlRoot, "defenseplays", "bank4_defense_special_pointers.yaml"));

        var formationSpawner = new FormationSpawner();
        var playSpawner = new PlaySpawner();

        var chosenOffPlay = playList.PlayList.First(p => (p.Slot ?? string.Empty).StartsWith("Pass", StringComparison.OrdinalIgnoreCase));
        var formationId = formationData.OffensiveFormations.Any(f => string.Equals(f.Id, chosenOffPlay.Formation, StringComparison.OrdinalIgnoreCase))
            ? chosenOffPlay.Formation
            : "00";

        var offense = formationSpawner.Spawn(
            world,
            formationData,
            formationId: formationId,
            teamIndex: 0,
            isOffense: true,
            playerControlled: false);

        // Placeholder defense: inline a minimal 11-man unit (stable coords).
        var defenseIds = new System.Collections.Generic.List<int>(11)
        {
            SpawnDefender(world, teamIndex: 1, new Vector2(170, 76), PlayerRole.DL, slot: "RE"),
            SpawnDefender(world, teamIndex: 1, new Vector2(170, 100), PlayerRole.DL, slot: "DT"),
            SpawnDefender(world, teamIndex: 1, new Vector2(170, 124), PlayerRole.DL, slot: "NT"),
            SpawnDefender(world, teamIndex: 1, new Vector2(170, 148), PlayerRole.DL, slot: "LE"),
            SpawnDefender(world, teamIndex: 1, new Vector2(190, 92), PlayerRole.LB, slot: "ROLB"),
            SpawnDefender(world, teamIndex: 1, new Vector2(192, 112), PlayerRole.LB, slot: "MLB"),
            SpawnDefender(world, teamIndex: 1, new Vector2(190, 132), PlayerRole.LB, slot: "LOLB"),
            SpawnDefender(world, teamIndex: 1, new Vector2(210, 70), PlayerRole.DB, slot: "RCB"),
            SpawnDefender(world, teamIndex: 1, new Vector2(210, 154), PlayerRole.DB, slot: "LCB"),
            SpawnDefender(world, teamIndex: 1, new Vector2(222, 104), PlayerRole.DB, slot: "FS"),
            SpawnDefender(world, teamIndex: 1, new Vector2(222, 120), PlayerRole.DB, slot: "SS"),
        };

        // Attach play assignments (routes, etc.) for determinism.
        playSpawner.Spawn(
            world,
            playList,
            defensePlays,
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseIds);

        var qbId = offense.Players.First(p => p.Role == PlayerRole.QB).EntityId;

        // Best-effort center id: any OL slot containing 'OC' (original center) or 'C'.
        var centerId = offense.Players
            .Where(p => p.Role == PlayerRole.OL)
            .Select(p => p.EntityId)
            .FirstOrDefault(id =>
            {
                var e = world.GetEntity(id);
                if (e.Has<PlayerAttributesComponent>())
                {
                    var pos = (e.Get<PlayerAttributesComponent>().Position ?? string.Empty).Trim().ToUpperInvariant();
                    if (pos.Contains("OC") || pos == "C")
                        return true;
                }

                if (e.Has<PlayerRoleComponent>())
                {
                    var slot = (e.Get<PlayerRoleComponent>().Slot ?? string.Empty).Trim().ToUpperInvariant();
                    if (slot.Contains("OC") || slot == "C")
                        return true;
                }

                return false;
            });

        if (centerId == 0)
            centerId = qbId;

        return new ScrimmageScenario(QbId: qbId, CenterId: centerId, BallId: ballId);
    }

    private static int SpawnDefender(World world, int teamIndex, Vector2 pos, PlayerRole role, string slot)
    {
        var id = PlayerEntityFactory.CreatePlayer(world, pos, teamIndex, isPlayerControlled: false, isOffense: false);
        var e = world.GetEntity(id);
        e.Attach(new PlayerRoleComponent(role, slot));
        return id;
    }

    private static void PrintControlled(World world, ControlState controlState)
    {
        if (controlState.ControlledEntityId is null)
        {
            Console.WriteLine("  control: (none)");
            return;
        }

        var id = controlState.ControlledEntityId.Value;
        var e = world.GetEntity(id);
        var team = e.Get<TeamComponent>();

        string role = controlState.Role.ToString();
        if (e.Has<PlayerAttributesComponent>())
        {
            var pos = (e.Get<PlayerAttributesComponent>().Position ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(pos))
                role = $"{role}/{pos}";
        }

        var vel = e.Get<VelocityComponent>().Velocity;
        var speed = vel.Length();

        var moveAction = "none";
        if (e.Has<MovementActionComponent>())
        {
            var a = e.Get<MovementActionComponent>();
            moveAction = $"{a.State} t={a.StateTimer:0.00}s cd={a.CooldownTimer:0.00}s";
        }

        var lastCmd = "none";
        if (e.Has<PlayerActionStateComponent>())
        {
            var a = e.Get<PlayerActionStateComponent>();
            lastCmd = a.LastAppliedTargetEntityId is null
                ? a.LastAppliedCommand.ToString()
                : $"{a.LastAppliedCommand} -> {a.LastAppliedTargetEntityId.Value}";
        }

        Console.WriteLine($"  control: id={id} team={team.TeamIndex} offense={team.IsOffense} role={role} speed={speed:0.000} vel=({vel.X:0.000},{vel.Y:0.000}) moveAction={moveAction} lastCmd={lastCmd}");
    }

    private static void PrintEntity(World world, string label, int entityId)
    {
        var e = world.GetEntity(entityId);
        var pos = e.Get<PositionComponent>().Position;
        var team = e.Get<TeamComponent>();
        var ball = e.Get<BallCarrierComponent>().HasBall;
        var ctrl = e.Get<PlayerControlComponent>().IsControlled;

        Console.WriteLine($"  {label} id={entityId} team={team.TeamIndex} offense={team.IsOffense} ball={ball} ctrl={ctrl} pos=({pos.X:0.0},{pos.Y:0.0})");
    }

    private static void PrintBall(World world, int ballEntityId)
    {
        var e = world.GetEntity(ballEntityId);
        var pos = e.Get<PositionComponent>().Position;

        var state = e.Get<BallStateComponent>().State;
        var owner = e.Get<BallOwnerComponent>().OwnerEntityId;
        var ownerStr = owner is null ? "none" : owner.Value.ToString(CultureInfo.InvariantCulture);

        var flightStr = string.Empty;
        if (e.Has<BallFlightComponent>())
        {
            var f = e.Get<BallFlightComponent>();
            if (f.Kind != BallFlightKind.None)
            {
                var meta = string.Empty;
                if (f.Kind == BallFlightKind.Pass)
                {
                    var passer = f.PasserId is null ? "none" : f.PasserId.Value.ToString(CultureInfo.InvariantCulture);
                    var target = f.TargetId is null ? "none" : f.TargetId.Value.ToString(CultureInfo.InvariantCulture);
                    meta = $" passer={passer} target={target} type={f.PassType}";
                }

                flightStr = $" flight={f.Kind} {f.ElapsedSeconds:0.000}/{f.DurationSeconds:0.000}s h={f.Height:0.00} apex={f.ApexHeight:0.00} complete={f.IsComplete}{(string.IsNullOrEmpty(meta) ? string.Empty : " " + meta)}";
            }
        }

        Console.WriteLine($"  ball id={ballEntityId} state={state} owner={ownerStr} pos=({pos.X:0.0},{pos.Y:0.0}){flightStr}");
    }

    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        var s = GetStringArg(args, name, null);
        return s is null ? defaultValue : int.Parse(s, CultureInfo.InvariantCulture);
    }

    private static string? GetStringArg(string[] args, string name, string? defaultValue)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }

        return defaultValue;
    }

    private static string FindYamlRoot()
    {
        // Prefer runtime YAML location: Content/Data
        var cwd = Directory.GetCurrentDirectory();
        var probe = Path.Combine(cwd, "Content", "Data");
        if (Directory.Exists(probe))
            return probe;

        // Back-compat: older layouts used a top-level 'content/' folder
        probe = Path.Combine(cwd, "content");
        if (Directory.Exists(probe))
            return probe;

        // Walk up from executable dir
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            probe = Path.Combine(dir.FullName, "Content", "Data");
            if (Directory.Exists(probe))
                return probe;

            probe = Path.Combine(dir.FullName, "content");
            if (Directory.Exists(probe))
                return probe;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate YAML root ('Content/Data' or 'content'). Pass --content <path-to-yaml-root>." );
    }
}
