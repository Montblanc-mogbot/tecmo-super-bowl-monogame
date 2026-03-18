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

    public Sim()
    {
        World = World.Create();
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
    }

    public readonly record struct PendingPlaySelection(
        int PlayNumber,
        string FormationId,
        string OffensivePlayName,
        string OffensivePlaySlot);
}
