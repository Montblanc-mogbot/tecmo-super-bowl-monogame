using System;
using Arch.Core;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch;

/// <summary>
/// Arch-based simulation entrypoint.
///
/// Owns the Arch World and provides a small API surface for game/UI code.
///
/// NOTE: Keep simulation deterministic: update via fixed timestep.
/// UI should call ApplyPlaySelection which queues intent to be applied on the next Update tick.
/// </summary>
public sealed class Sim : IDisposable
{
    public World World { get; private set; }

    public SimSnapshot Snapshot { get; } = new();

    private PendingPlaySelection? _pendingSelection;

    private readonly Systems.MovementSystem _movement = new();

    private readonly System.Collections.Generic.List<int> _offense = new(11);
    private readonly System.Collections.Generic.List<int> _defense = new(11);
    private int _ballEntityId;

    public Sim()
    {
        World = World.Create();
        BootstrapDemoWorld();
    }

    public void Dispose()
    {
        World.Dispose();
    }

    public void Reset()
    {
        // TODO: if Arch exposes a clear/reset pattern, prefer that.
        // For now: recreate world (simple and deterministic for early refactor stage).
        World.Dispose();
        World = World.Create();
        Snapshot.Tick = 0;
        _pendingSelection = null;

        _offense.Clear();
        _defense.Clear();
        _ballEntityId = 0;

        BootstrapDemoWorld();
    }

    public void ApplyPlaySelection(in PendingPlaySelection sel)
    {
        _pendingSelection = sel;

        var e = new PlaySelectedEvent(sel.PlayNumber, sel.FormationId, sel.OffensivePlayName, sel.OffensivePlaySlot);
        SimEventBus.Send(ref e);
    }

    public void Update(float dtSeconds)
    {
        // Apply queued selection at the start of a tick.
        if (_pendingSelection is not null)
        {
            var sel = _pendingSelection.Value;
            _pendingSelection = null;

            // TODO: call SimArch spawners to attach scripts/routes based on selection.
            Console.WriteLine($"[sim-arch] apply play selection play_number={sel.PlayNumber} formation={sel.FormationId}");
        }

        // Run systems (minimal set for now).
        _movement.Update(World, dtSeconds);

        // Update snapshot.
        Snapshot.Tick++;
        UpdateSnapshot();
    }

    private void BootstrapDemoWorld()
    {
        var (off, def, ball) = Spawning.FormationSpawner.SpawnDemoScrimmage(World);
        _offense.AddRange(off);
        _defense.AddRange(def);
        _ballEntityId = ball;
    }

    private void UpdateSnapshot()
    {
        // Collect players
        var players = new SimSnapshot.PlayerSnapshot[_offense.Count + _defense.Count];
        var idx = 0;

        void Fill(int entityId)
        {
            // Query by entity id (use World.Get with an Entity wrapper)
            var e = new Arch.Core.Entity(World, entityId);
            if (!e.IsAlive())
                return;

            if (!e.Has<Components.Position>() || !e.Has<Components.Team>())
                return;

            var pos = e.Get<Components.Position>().Value;
            var team = e.Get<Components.Team>();

            players[idx] = new SimSnapshot.PlayerSnapshot
            {
                EntityId = entityId,
                Position = pos,
                TeamIndex = team.TeamIndex,
                IsOffense = team.IsOffense,
                HasBall = false,
                SpriteId = team.IsOffense ? "qb" : "def",
            };
            idx++;
        }

        foreach (var id in _offense) Fill(id);
        foreach (var id in _defense) Fill(id);

        if (idx != players.Length)
            Array.Resize(ref players, idx);

        Snapshot.Players = players;

        // Ball
        var ballEntity = new Arch.Core.Entity(World, _ballEntityId);
        if (ballEntity.IsAlive() && ballEntity.Has<Components.Position>() && ballEntity.Has<Components.Ball>())
        {
            var bpos = ballEntity.Get<Components.Position>().Value;
            var b = ballEntity.Get<Components.Ball>();
            Snapshot.Ball = new SimSnapshot.BallSnapshot
            {
                Position = bpos,
                IsHeld = b.State == TecmoSBGame.State.BallState.Held,
                OwnerEntityId = b.OwnerEntityId,
                SpriteId = "ball",
            };

            // Mark owner in players snapshot
            if (b.OwnerEntityId != 0)
            {
                for (var i = 0; i < Snapshot.Players.Length; i++)
                {
                    if (Snapshot.Players[i].EntityId == b.OwnerEntityId)
                    {
                        Snapshot.Players[i].HasBall = true;
                        break;
                    }
                }
            }
        }
        else
        {
            Snapshot.Ball = default;
        }
    }

    public readonly record struct PendingPlaySelection(
        int PlayNumber,
        string FormationId,
        string OffensivePlayName,
        string OffensivePlaySlot);
}
