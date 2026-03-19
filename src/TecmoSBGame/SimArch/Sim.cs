using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSB;
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

    private TecmoSB.PlayListConfig? _playList;
    private TecmoSB.PlayDataConfig? _playData;

    private readonly Systems.MovementSystem _movement = new();
    private readonly Systems.PlayScriptSystem _playScripts = new();
    private readonly Systems.PreSnapSystems _preSnap = new();
    private readonly Systems.BallSystem _ball = new();
    private readonly Systems.PassFlightCompleteSystem _passComplete = new();
    private readonly Systems.KickoffFlightCompleteSystem _kickoffComplete = new();
    private readonly Systems.TackleAndPlayEndSystems _tackleAndEnd = new();

    private readonly System.Collections.Generic.List<int> _offense = new(11);
    private readonly System.Collections.Generic.List<int> _defense = new(11);
    private int _ballEntityId = -1;

    private Components.Control _control;
    private Components.Input _input;

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
        _ballEntityId = -1;

        BootstrapDemoWorld();
    }

    public void ApplyPlaySelection(in PendingPlaySelection sel)
    {
        _pendingSelection = sel;

        var e = new PlaySelectedEvent(sel.PlayNumber, sel.FormationId, sel.OffensivePlayName, sel.OffensivePlaySlot);
        SimEventBus.Send(ref e);
    }

    public void SetInput(Microsoft.Xna.Framework.Vector2 direction)
    {
        _input.Direction = direction;
    }

    public void Update(float dtSeconds)
    {
        // Apply queued selection at the start of a tick.
        if (_pendingSelection is not null)
        {
            var sel = _pendingSelection.Value;
            _pendingSelection = null;

            // Apply play selection.
            if (_playData is null)
                throw new InvalidOperationException("SimArch PlayData not loaded");

            Spawning.PlaySpawner.ApplyPlay(World, _playData, _offense, _defense, _ballEntityId, sel.PlayNumber);
        }

        // Run systems (minimal set for now).
        // Pre-snap placement runs opportunistically when the ball is Dead.
        _preSnap.Update(World, _offense, _defense, _ballEntityId, offenseDirSign: 1f);

        _playScripts.Update(World, dtSeconds, _ballEntityId, ref _control);
        _movement.Update(World, dtSeconds, _control.ControlledEntityId, _input.Direction);
        _ball.Update(World, dtSeconds);
        _passComplete.Update(World);
        _kickoffComplete.Update(World);
        _tackleAndEnd.Update(World, _ballEntityId, ref _control);

        // Update snapshot.
        Snapshot.Tick++;
        UpdateSnapshot();
    }

    private void BootstrapDemoWorld()
    {
        // Arch can allocate entity ids starting at 0; our gameplay code uses 0 as a "null" sentinel.
        // Ensure demo roster ids start at 1+.
        _ = World.Create();

        // Formation YAML driven spawn (fallbacks to legacy demo roster if YAML not available).
        TecmoSB.FormationDataConfig? formationData = null;
        try
        {
            formationData = TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sim-arch] WARN: could not load formation_data.yaml; using demo roster. err={ex.Message}");
        }

        // Playbook YAML
        try
        {
            _playList = PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
            _playData = PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sim-arch] WARN: could not load playbook YAML; play selection will be limited. err={ex.Message}");
            _playList = null;
            _playData = null;
        }

        var (off, def, ball) = formationData is null
            ? Spawning.FormationSpawner.SpawnDemoScrimmage(World)
            : Spawning.FormationSpawner.SpawnScrimmage(World, formationData);
        _offense.AddRange(off);
        _defense.AddRange(def);
        _ballEntityId = ball;

        _control = new Components.Control { ControlledEntityId = off.Count > 0 ? off[0] : -1, PendingForcedEntityId = -1 };
    }

    private void UpdateSnapshot()
    {
        // Collect players
        var players = new SimSnapshot.PlayerSnapshot[_offense.Count + _defense.Count];
        var idx = 0;

        // Build a lookup for current sim entity snapshots.
        var lookup = new System.Collections.Generic.Dictionary<int, (Microsoft.Xna.Framework.Vector2 pos, Components.Team team)>();
        var qPlayers = new QueryDescription().WithAll<Components.Position, Components.Team>();
        World.Query(in qPlayers, (Entity e, ref Components.Position p, ref Components.Team t) =>
        {
            lookup[e.Id] = (p.Value, t);
        });

        void Fill(int entityId)
        {
            if (!lookup.TryGetValue(entityId, out var v))
                return;

            players[idx] = new SimSnapshot.PlayerSnapshot
            {
                EntityId = entityId,
                Position = v.pos,
                TeamIndex = v.team.TeamIndex,
                IsOffense = v.team.IsOffense,
                HasBall = false,
                SpriteId = v.team.IsOffense ? "qb" : "def",
            };
            idx++;
        }

        foreach (var id in _offense) Fill(id);
        foreach (var id in _defense) Fill(id);

        if (idx != players.Length)
            Array.Resize(ref players, idx);

        Snapshot.Players = players;

        // Ball
        {
            // Ball
            var qBall = new QueryDescription().WithAll<Components.Ball, Components.Position>();
            var didSetBall = false;

            World.Query(in qBall, (Entity e, ref Components.Ball b, ref Components.Position p) =>
            {
                if (e.Id != _ballEntityId)
                    return;

                Snapshot.Ball = new SimSnapshot.BallSnapshot
                {
                    Position = p.Value,
                    IsHeld = b.State == TecmoSBGame.SimArch.Components.BallState.Held,
                    OwnerEntityId = b.OwnerEntityId,
                    SpriteId = "ball",
                };
                didSetBall = true;
            });

            if (didSetBall)
            {
                var b = Snapshot.Ball;

                // Mark owner in players snapshot
                if (b.OwnerEntityId >= 0)
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

                return;
            }
        }

        Snapshot.Ball = default;
    }

    public readonly record struct PendingPlaySelection(
        int PlayNumber,
        string FormationId,
        string OffensivePlayName,
        string OffensivePlaySlot);
}
