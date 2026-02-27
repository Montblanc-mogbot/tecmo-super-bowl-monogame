using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Events;
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
        var formationSpawner = new FormationSpawner();

        var gameState = new GameStateSystem(match, play, events, formationData: formationData, formationSpawner: formationSpawner, headlessAutoAdvance: true);

        var world = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            .AddSystem(gameState)
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new CollisionContactSystem(events))
            .AddSystem(new EngagementSystem(events))
            .AddSystem(new TackleInterruptSystem(events))
            .AddSystem(new TackleResolutionSystem(events, match, play))
            .AddSystem(new BehaviorStackSystem())
            .AddSystem(new ContactDebugLogSystem(events))
            .Build();

        var ids = gameState.SpawnKickoffScenario(world);

        Console.WriteLine("[headless] kickoff roster:");
        foreach (var id in ids.AllEntityIds)
        {
            var e = world.GetEntity(id);
            var pos = e.Get<TecmoSBGame.Components.PositionComponent>()?.Position;
            var role = e.Get<TecmoSBGame.Components.PlayerRoleComponent>()?.Role;

            if (pos is null || role is null)
                continue;

            Console.WriteLine($"  id={id,4} role={role,-7} pos=({pos.Value.X,6:0.0},{pos.Value.Y,6:0.0})");
        }

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
}
