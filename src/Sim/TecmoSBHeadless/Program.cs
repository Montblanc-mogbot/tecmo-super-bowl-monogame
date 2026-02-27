using System.Globalization;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;
using TecmoSBGame.Timing;

namespace TecmoSBHeadless;

internal static class Program
{
    // (Fixed-step utilities live in TecmoSBGame.Timing)

    public static int Main(string[] args)
    {
        var ticks = GetIntArg(args, "--ticks", 600);
        var hz = GetIntArg(args, "--hz", 60);
        var yamlRoot = GetStringArg(args, "--content", null) ?? FindYamlRoot();

        Console.WriteLine($"[Headless] YAML root: {yamlRoot}");
        var yaml = LoadYamlContent(yamlRoot);

        var events = new GameEvents();
        var matchState = new MatchState();
        var playState = new PlayState();
        var gameState = new GameStateSystem(matchState, playState, events, headlessAutoAdvance: true);

        var loopState = new LoopState(new GameLoopMachine(yaml.GameLoop), new OnFieldLoopMachine(yaml.OnFieldLoop));
        var controlState = new ControlState();

        var world = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(new PlayerControlSystem(controlState, loopState, enableInput: false))
            .AddSystem(new ActionResolutionSystem(events, matchState, playState))
            .AddSystem(gameState)
            .AddSystem(new WhistleOnTackleSystem(events))
            .AddSystem(new LoopMachineSystem(loopState, events))
            .Build();

        var scenario = gameState.SpawnKickoffScenario(world);

        var fixedStep = new FixedTimestepRunner(hz, maxTicksPerFrame: int.MaxValue, maxAccumulated: TimeSpan.FromDays(1));
        for (var i = 0; i < ticks; i++)
        {
            events.BeginTick();
            fixedStep.TickOnce(world.Update);

            // Snapshot at start, once per second, and at end.
            if (i == 0 || (i + 1) % hz == 0 || i == ticks - 1)
            {
                PrintSummary(world, gameState, loopState, controlState, i + 1, hz, scenario);
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

    private static void PrintSummary(World world, GameStateSystem gameState, LoopState loopState, ControlState controlState, int tick, int hz, GameStateSystem.KickoffScenarioIds scenario)
    {
        var t = (tick / (double)hz).ToString("0.000", CultureInfo.InvariantCulture);
        Console.WriteLine($"[t={t}s tick={tick}] phase={gameState.CurrentPhase} phaseTimer={gameState.PhaseTimer:0.000}");
        Console.WriteLine($"  loops: game={loopState.GameLoopStateId} onField={loopState.OnFieldStateId} (onFieldTimer={loopState.OnFieldSecondsInState:0.000}s)");
        Console.WriteLine($"  match: {gameState.MatchState.ToSummaryString()}");
        Console.WriteLine($"  play:  {gameState.PlayState.ToSummaryString()}");

        PrintControlled(world, controlState);

        PrintEntity(world, "kicker", scenario.KickerId);
        PrintEntity(world, "returner", scenario.ReturnerId);

        int? ballCarrier = null;
        foreach (var id in scenario.AllEntityIds)
        {
            if (world.GetEntity(id).Get<BallCarrierComponent>().HasBall)
            {
                ballCarrier = id;
                break;
            }
        }

        if (ballCarrier is not null)
            PrintEntity(world, "ball", ballCarrier.Value);

        Console.WriteLine();
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
