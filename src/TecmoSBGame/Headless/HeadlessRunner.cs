using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSBGame.Events;
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

        var gameState = new GameStateSystem(match, play, events, headlessAutoAdvance: true);

        var world = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(gameState)
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new HeadlessContactSeederSystem())
            .AddSystem(new CollisionContactSystem(events))
            .AddSystem(new ContactDebugLogSystem(events))
            .Build();

        gameState.SpawnKickoffScenario(world);

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
