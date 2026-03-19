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


    private readonly Systems.MovementSystem _movement = new();
    private readonly Systems.CollisionContactSystem _contacts = new();
    private readonly Systems.EngagementSystem _engagement = new();
    private readonly Systems.TackleResolutionSystem _tackleResolution = new();
    private readonly Systems.BehaviorStackSystem _behaviorStack = new();
    private readonly PlayScripts.PlayScriptRegistry _scriptRegistry = new();
    private readonly Routes.RouteRegistry _routeRegistry = new();
    private readonly Systems.PlayScriptSystem _playScripts = new();
    private readonly Systems.RouteFollowSystem _routes = new();
    private readonly Systems.BlockerAiSystem _blockerAi = new();
    private readonly Systems.DefensiveRushSystem _rush = new();
    private readonly Systems.CoverageSystem _coverage = new();
    private readonly Systems.QbAiSystem _qbAi = new();

    // Match/play rules
    private readonly SimArch.State.MatchState _match = new();
    private readonly SimArch.State.PlayState _play = new();

    public SimArch.State.MatchState MatchState => _match;
    public SimArch.State.PlayState PlayState => _play;
    private readonly Systems.GameClockSystem _clock = new();
    private readonly Systems.DownDistanceSystem _downDistance = new();
    private readonly Systems.PlayLifecycleSystem _lifecycle;
    private readonly Systems.PreSnapSystems _preSnap = new();
    private readonly Systems.BallSystem _ball = new();
    private readonly Systems.PassFlightCompleteSystem _passComplete = new();
    private readonly Systems.KickoffFlightCompleteSystem _kickoffComplete = new();
    private readonly Systems.TackleAndPlayEndSystems _tackleAndEnd = new(); // legacy fallback; no longer detects tackles

    private readonly System.Collections.Generic.List<int> _offense = new(11);
    private readonly System.Collections.Generic.List<int> _defense = new(11);
    private int _ballEntityId = -1;

    private Components.Control _control;
    private Components.Input _input;

    private TecmoSB.FormationDataConfig? _formationData;
    private TecmoSB.PlayDataConfig? _playData;

    public Sim(TecmoSB.FormationDataConfig? formationData = null, TecmoSB.PlayDataConfig? playData = null)
    {
        _formationData = formationData;
        _playData = playData;

        _lifecycle = new Systems.PlayLifecycleSystem(_match, _play);

        World = World.Create();
        BootstrapWorld();
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

        _scriptRegistry.Clear();
        _routeRegistry.Clear();

        _offense.Clear();
        _defense.Clear();
        _ballEntityId = -1;

        _match.Quarter = 1;
        _match.GameClockSeconds = 5 * 60;
        _match.PossessionTeam = 0;
        _match.OffenseDirection = SimArch.State.OffenseDirection.LeftToRight;
        _match.Down = 1;
        _match.YardsToGo = 10;
        _match.BallSpot = SimArch.State.BallSpot.Own(25);
        _match.Team0Score = 0;
        _match.Team1Score = 0;
        _match.PlayNumber = 0;
        _match.DriveId = 0;
        _match.MatchOver = false;

        _play.ResetForNewPlay(playId: 0, startAbsoluteYard: SimArch.State.PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection));

        BootstrapWorld();
    }

    public void ApplyPlaySelection(in PendingPlaySelection sel)
    {
        _pendingSelection = sel;

        // Treat selection as starting a new play from the current match spot.
        _play.ResetForNewPlay(
            playId: _match.PlayNumber + 1,
            startAbsoluteYard: SimArch.State.PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection));

        var e = new PlaySelectedEvent(
            OffensiveFormationId: sel.FormationId,
            OffensivePlayName: sel.OffensivePlayName,
            OffensivePlaySlot: sel.OffensivePlaySlot,
            OffensivePlayNumber: sel.PlayNumber,
            DefensiveCallId: string.Empty);
        SimEventBus.Send(ref e);
    }

    /// <summary>
    /// Advances to the next down's pre-snap state using the current match spot.
    /// Call this from a host UI after the post-play summary is acknowledged.
    /// </summary>
    public void AdvanceToNextPlay()
    {
        // Deprecated: lifecycle system now auto-advances; keep for transitional callers.
        _play.ResetForNewPlay(
            playId: _match.PlayNumber + 1,
            startAbsoluteYard: SimArch.State.PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection));
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

            _scriptRegistry.Clear();
            _routeRegistry.Clear();
            Spawning.PlaySpawner.ApplyPlay(World, _playData, _offense, _defense, _ballEntityId, sel.PlayNumber, _scriptRegistry, _routeRegistry);
        }

        // Run systems (minimal set for now).
        // Pre-snap placement runs opportunistically when the ball is Dead.
        _preSnap.Update(World, _offense, _defense, _ballEntityId, offenseDirSign: 1f);

        _playScripts.Update(World, dtSeconds, _ballEntityId, _offense, _defense, _scriptRegistry, ref _control);
        _routes.Update(World, dtSeconds, _routeRegistry);
        _blockerAi.Update(World, dtSeconds, _offense, _defense, _ballEntityId);
        _rush.Update(World, dtSeconds, _defense);
        _coverage.Update(World, dtSeconds, _ballEntityId, _defense);
        _qbAi.Update(World, dtSeconds, _ballEntityId);
        _movement.Update(World, dtSeconds, _control.ControlledEntityId, _input.Direction);
        _contacts.Update(World, _offense, _defense);
        _engagement.Update(World, dtSeconds);
        _tackleResolution.Update(World, dtSeconds, _ballEntityId, ref _control);
        _behaviorStack.Update(World, dtSeconds);
        _ball.Update(World, dtSeconds);


        // Convert sim whistle into lifecycle events.
        if (_tackleResolution.WhistledThisTick)
        {
            // TODO: compute absolute yards from world positions + ball spot.
            // For now, keep it conservative (0 yards) to avoid bad state jumps.
            _play.EndAbsoluteYard = _play.StartAbsoluteYard;
            _play.Result = new TecmoSBGame.SimArch.State.PlayResult(
                YardsGained: 0,
                Turnover: false,
                Touchdown: false,
                Safety: false);

            var w = new TecmoSBGame.SimArch.Events.WhistleEvent("tackle");
            SimEventBus.Send(ref w);

            var ended = new TecmoSBGame.SimArch.Events.PlayEndedEvent(
                PlayId: _play.PlayId,
                Reason: (int)TecmoSBGame.SimArch.State.WhistleReason.Tackle,
                EndAbsoluteYard: _play.EndAbsoluteYard,
                YardsGained: _play.Result.YardsGained,
                Turnover: _play.Result.Turnover,
                Touchdown: _play.Result.Touchdown,
                Safety: _play.Result.Safety);
            SimEventBus.Send(ref ended);

            // Apply match rules immediately (still deterministic) using the play model.
            _downDistance.ApplyPlayEnd(_match, _play);
        }

        // Lifecycle transitions (auto-snap + auto-advance for now).
        _lifecycle.Update(World);

        // Play time/clock tick (after lifecycle transitions).
        if (_play.Phase == TecmoSBGame.SimArch.State.PlayPhase.InPlay)
        {
            _play.PlayElapsedSeconds += dtSeconds;
            _clock.Update(_match, _play);
        }
        _passComplete.Update(World);
        _kickoffComplete.Update(World);
        // NOTE: tackle detection is now handled by CollisionContactSystem + TackleResolutionSystem.
        // Keep this system wired only for any remaining play-end/reset scaffolding.
        // _tackleAndEnd.Update(World, _ballEntityId, ref _control);

        // Update snapshot.
        Snapshot.Tick++;
        UpdateSnapshot();
    }

    private void BootstrapWorld()
    {
        // Arch can allocate entity ids starting at 0; our gameplay code uses 0 as a "null" sentinel.
        // Ensure roster ids start at 1+.
        _ = World.Create();

        // If the host didn't provide YAML configs (e.g. quick headless), try to load from repo content.
        if (_formationData is null)
        {
            try
            {
                _formationData = TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sim-arch] WARN: could not load formation_data.yaml; using demo roster. err={ex.Message}");
            }
        }

        if (_playData is null)
        {
            try
            {
                _playData = TecmoSB.PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sim-arch] WARN: could not load playdata YAML; play selection will be limited. err={ex.Message}");
                _playData = null;
            }
        }

        var (off, def, ball) = _formationData is null
            ? Spawning.FormationSpawner.SpawnDemoScrimmage(World)
            : Spawning.FormationSpawner.SpawnScrimmage(World, _formationData);
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

        // Lookups
        var posById = new System.Collections.Generic.Dictionary<int, Microsoft.Xna.Framework.Vector2>();
        var teamById = new System.Collections.Generic.Dictionary<int, Components.Team>();
        var roleById = new System.Collections.Generic.Dictionary<int, Components.Role>();
        var behaviorById = new System.Collections.Generic.Dictionary<int, Components.Behavior>();

        var qPlayers = new QueryDescription().WithAll<Components.Position, Components.Team>();
        World.Query(in qPlayers, (Entity e, ref Components.Position p, ref Components.Team t) =>
        {
            posById[e.Id] = p.Value;
            teamById[e.Id] = t;
        });

        var qRole = new QueryDescription().WithAll<Components.Role>();
        World.Query(in qRole, (Entity e, ref Components.Role r) => roleById[e.Id] = r);

        var qBeh = new QueryDescription().WithAll<Components.Behavior>();
        World.Query(in qBeh, (Entity e, ref Components.Behavior b) => behaviorById[e.Id] = b);

        void Fill(int entityId)
        {
            if (!posById.TryGetValue(entityId, out var pos))
                return;
            if (!teamById.TryGetValue(entityId, out var team))
                return;

            roleById.TryGetValue(entityId, out var role);
            behaviorById.TryGetValue(entityId, out var beh);

            players[idx] = new SimSnapshot.PlayerSnapshot
            {
                EntityId = entityId,
                Position = pos,
                TeamIndex = team.TeamIndex,
                IsOffense = team.IsOffense,
                HasBall = false,
                SpriteId = team.IsOffense ? "qb" : "def",
                Role = role.Id.ToString(),
                Behavior = beh.State.ToString(),
            };
            idx++;
        }

        foreach (var id in _offense) Fill(id);
        foreach (var id in _defense) Fill(id);

        if (idx != players.Length)
            Array.Resize(ref players, idx);

        Snapshot.Players = players;

        // Engagement lines (from Engagement component partner pairs)
        {
            var lines = new System.Collections.Generic.List<SimSnapshot.EngagementLine>();
            var qEng = new QueryDescription().WithAll<Components.Engagement>();
            World.Query(in qEng, (Entity e, ref Components.Engagement eng) =>
            {
                if (eng.PartnerEntityId < 0)
                    return;

                // Only draw one line per pair.
                if (e.Id > eng.PartnerEntityId)
                    return;

                if (!posById.TryGetValue(e.Id, out var aPos))
                    return;
                if (!posById.TryGetValue(eng.PartnerEntityId, out var bPos))
                    return;

                lines.Add(new SimSnapshot.EngagementLine(e.Id, eng.PartnerEntityId, aPos, bPos));
            });

            Snapshot.EngagementLines = lines.ToArray();
        }

        // Route debug
        {
            var routes = new System.Collections.Generic.List<SimSnapshot.RouteDebug>();
            var qRoutes = new QueryDescription().WithAll<Components.RouteFollow, Components.Behavior>();
            World.Query(in qRoutes, (Entity e, ref Components.RouteFollow rf, ref Components.Behavior b) =>
            {
                routes.Add(new SimSnapshot.RouteDebug(
                    EntityId: e.Id,
                    TargetPosition: b.TargetPosition,
                    NodeIndex: rf.NodeIndex,
                    FramesRemaining: rf.FramesRemainingInNode,
                    Completed: rf.Completed));
            });

            Snapshot.Routes = routes.ToArray();
        }

        // Coverage debug
        {
            var cov = new System.Collections.Generic.List<SimSnapshot.CoverageDebug>();
            var qCov = new QueryDescription().WithAll<Components.Coverage>();
            World.Query(in qCov, (Entity e, ref Components.Coverage c) =>
            {
                cov.Add(new SimSnapshot.CoverageDebug(
                    DefenderId: e.Id,
                    Type: (SnapshotCoverageType)c.Type,
                    AssignmentTargetId: c.AssignmentTargetId,
                    PursuitTargetId: c.PursuitTargetId,
                    InPursuit: c.InPursuit,
                    Landmark: c.LandmarkPosition));
            });

            Snapshot.Coverage = cov.ToArray();
        }

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
