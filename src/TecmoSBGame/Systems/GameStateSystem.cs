using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Factories;
using TecmoSBGame.Spawning;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Manages game state for a single playable slice (kickoff scenario).
/// Handles kickoff setup, receiving, and tackle resolution.
/// </summary>
public class GameStateSystem : EntityUpdateSystem
{
    private readonly bool _headlessAutoAdvance;
    private readonly GameEvents? _events;
    private readonly MatchState _matchState;
    private readonly PlayState _playState;
    private readonly FormationDataConfig? _formationData;
    private readonly FormationSpawner? _formationSpawner;
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<VelocityComponent> _velocityMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BehaviorComponent> _behaviorMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    private ComponentMapper<SpriteComponent> _spriteMapper;
    private ComponentMapper<BallStateComponent> _ballStateMapper;
    private ComponentMapper<BallOwnerComponent> _ballOwnerMapper;
    private ComponentMapper<BallFlightComponent> _ballFlightMapper;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.KickoffSetup;
    public float PhaseTimer { get; private set; } = 0f;
    public int KickingTeam { get; private set; } = 0;
    public int ReceivingTeam { get; private set; } = 1;

    public MatchState MatchState => _matchState;
    public PlayState PlayState => _playState;

    private World? _world;

    // Kickoff slice bookkeeping so we can retag teams when a kickoff is set up after score.
    private readonly List<int> _kickingEntityIds = new();
    private readonly List<int> _receivingEntityIds = new();

    private int _ballCarrierId = -1;
    private int _kickerId = -1;
    private int _ballEntityId = -1;
    private bool _ballKicked = false;
    private bool _ballCaught = false;
    private bool _tackleMade = false;

    public GameStateSystem(
        MatchState matchState,
        PlayState playState,
        GameEvents? events = null,
        FormationDataConfig? formationData = null,
        FormationSpawner? formationSpawner = null,
        bool headlessAutoAdvance = false)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _matchState = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _playState = playState ?? throw new ArgumentNullException(nameof(playState));
        _events = events;
        _formationData = formationData;
        _formationSpawner = formationSpawner;
        _headlessAutoAdvance = headlessAutoAdvance;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _velocityMapper = mapperService.GetMapper<VelocityComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
        _spriteMapper = mapperService.GetMapper<SpriteComponent>();
        _ballStateMapper = mapperService.GetMapper<BallStateComponent>();
        _ballOwnerMapper = mapperService.GetMapper<BallOwnerComponent>();
        _ballFlightMapper = mapperService.GetMapper<BallFlightComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // React to score->kickoff transitions.
        // (Use Read() so multiple systems can observe this setup event.)
        if (_events is not null)
        {
            var setups = _events.Read<KickoffSetupEvent>();
            if (setups.Count > 0)
            {
                var k = setups[^1];
                ApplyKickoffSetup(k.KickingTeam, k.ReceivingTeam);
            }
        }

        // Do not Drain whistles here: LoopMachineSystem + PlayEndSystem rely on observing them.
        // Instead, let PlayEndSystem finalize PlayState/MatchState.
        if (_playState.IsOver)
        {
            CurrentPhase = GamePhase.End;
            PhaseTimer = 0f;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        PhaseTimer += dt;

        // Keep PlayState updated for deterministic headless snapshots.
        _playState.PlayElapsedSeconds += dt;
        _playState.Phase = CurrentPhase switch
        {
            GamePhase.KickoffSetup => PlayPhase.PreSnap,
            GamePhase.End => PlayPhase.PostPlay,
            _ => PlayPhase.InPlay,
        };

        switch (CurrentPhase)
        {
            case GamePhase.KickoffSetup:
                UpdateKickoffSetup();
                break;

            case GamePhase.KickoffFlight:
                UpdateKickoffFlight();
                break;

            case GamePhase.Return:
                UpdateReturn();
                break;

            case GamePhase.Tackle:
                UpdateTackle();
                break;

            case GamePhase.End:
                if (_headlessAutoAdvance)
                {
                    // Deterministic auto-reset so headless runs don't stall.
                    if (PhaseTimer > 0.25f)
                        ResetKickoff();
                }
                else
                {
                    // Wait for restart input
                    if (Keyboard.GetState().IsKeyDown(Keys.Enter))
                    {
                        ResetKickoff();
                    }
                }
                break;
        }

        // Keep the dedicated ball entity in sync with the PlayState model.
        // Do this after phase/state transitions so downstream systems (BallPhysicsSystem) see the latest state.
        SyncBallModelToEntity();
    }

    private void UpdateKickoffSetup()
    {
        if (_headlessAutoAdvance)
        {
            // Deterministic auto-kick so headless runs don't depend on an input device/window.
            if (PhaseTimer > 0.10f)
                ExecuteKickoff();
        }
        else
        {
            // Wait for user to press space to kick
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                ExecuteKickoff();
            }
        }
    }

    private void ExecuteKickoff()
    {
        CurrentPhase = GamePhase.KickoffFlight;
        PhaseTimer = 0f;
        _ballKicked = true;

        _events?.Publish(new SnapEvent(ReceivingTeam, KickingTeam));

        _playState.BallState = BallState.InAir;
        _playState.BallOwnerEntityId = null;

        // Move the ball to kicked state.
        // (We track "has ball" only for players; the ball itself is a dedicated entity.)
        if (_kickerId != -1 && _ballMapper.Has(_kickerId))
            _ballMapper.Get(_kickerId).HasBall = false;
        if (_ballCarrierId != -1 && _ballMapper.Has(_ballCarrierId))
            _ballMapper.Get(_ballCarrierId).HasBall = false;

        // Kick trajectory tuning (keep simple + deterministic for now).
        const float fieldLeft = TecmoSBGame.Field.FieldBounds.FieldLeftX;
        const float fieldRight = TecmoSBGame.Field.FieldBounds.FieldRightX;
        const float kickoffHangtimeSeconds = 1.50f;
        const float kickoffApexHeight = 18.0f;
        const float kickoffForwardDistance = 140f;

        if (_ballEntityId != -1 && _positionMapper.Has(_ballEntityId))
        {
            var start = _kickerId != -1 && _positionMapper.Has(_kickerId)
                ? _positionMapper.Get(_kickerId).Position
                : _positionMapper.Get(_ballEntityId).Position;

            var end = new Vector2(
                MathHelper.Clamp(start.X + kickoffForwardDistance, fieldLeft, fieldRight),
                start.Y);

            // Place the ball at the kick origin immediately.
            _positionMapper.Get(_ballEntityId).Position = start;

            // Attach/overwrite the flight component for deterministic parametric motion.
            if (_ballFlightMapper.Has(_ballEntityId))
            {
                var f = _ballFlightMapper.Get(_ballEntityId);
                f.Kind = BallFlightKind.Kickoff;
                f.PasserId = null;
                f.TargetId = null;
                f.PassType = PassType.Bullet;
                f.StartPos = start;
                f.EndPos = end;
                f.DurationSeconds = kickoffHangtimeSeconds;
                f.ApexHeight = kickoffApexHeight;
                f.ElapsedSeconds = 0f;
                f.Height = 0f;
                f.IsComplete = false;
            }
            else
            {
                _world?.GetEntity(_ballEntityId)
                    .Attach(new BallFlightComponent(BallFlightKind.Kickoff, start, end, kickoffHangtimeSeconds, kickoffApexHeight));
            }

            // AI: Return team moves to catch position.
            for (var i = 0; i < _receivingEntityIds.Count; i++)
            {
                var entityId = _receivingEntityIds[i];
                if (!_behaviorMapper.Has(entityId))
                    continue;

                var behavior = _behaviorMapper.Get(entityId);
                behavior.State = BehaviorState.MovingToPosition;
                behavior.TargetPosition = end;
            }
        }
    }

    private void UpdateKickoffFlight()
    {
        // Kickoff flight completes when the parametric model reaches its end.
        if (_ballEntityId == -1 || !_ballFlightMapper.Has(_ballEntityId))
            return;

        var flight = _ballFlightMapper.Get(_ballEntityId);
        if (!flight.IsComplete)
            return;

        // Ball lands - check for catch.
        CurrentPhase = GamePhase.Return;
        PhaseTimer = 0f;

        // Assign ball to returner.
        for (var i = 0; i < _receivingEntityIds.Count; i++)
        {
            var entityId = _receivingEntityIds[i];
            if (!_teamMapper.Has(entityId) || !_ballMapper.Has(entityId))
                continue;

            var team = _teamMapper.Get(entityId);

            _ballCarrierId = entityId;
            _ballMapper.Get(entityId).HasBall = true;
            team.IsOffense = true;

            _playState.BallState = BallState.Held;
            _playState.BallOwnerEntityId = entityId;

            _events?.Publish(new BallCaughtEvent(entityId, _positionMapper.Get(entityId).Position));

            // Set player control.
            team.IsPlayerControlled = true;

            if (_behaviorMapper.Has(entityId))
            {
                var behavior = _behaviorMapper.Get(entityId);
                behavior.State = BehaviorState.Idle;
            }

            break;
        }

        // Keep the flight component attached for determinism; BallPhysicsSystem will ignore it while held.

        // Coverage team now pursues.
        for (var i = 0; i < _kickingEntityIds.Count; i++)
        {
            var entityId = _kickingEntityIds[i];
            if (!_behaviorMapper.Has(entityId))
                continue;

            var behavior = _behaviorMapper.Get(entityId);
            behavior.State = BehaviorState.TrackingPlayer;
            behavior.TargetEntityId = _ballCarrierId;
        }
    }

    private void UpdateReturn()
    {
        if (_ballCarrierId == -1)
            return;

        // Tackle detection/resolution now flows through:
        //   CollisionContactSystem -> TackleContactEvent -> TackleResolutionSystem -> WhistleEvent("tackle")
        //
        // Keep GameStateSystem focused on phase orchestration and ball model sync.
    }

    private void ExecuteTackle(int tacklerId)
    {
        CurrentPhase = GamePhase.Tackle;
        PhaseTimer = 0f;
        _tackleMade = true;

        if (_ballCarrierId != -1)
        {
            var tacklePos = _positionMapper.Get(_ballCarrierId).Position;
            _events?.Publish(new TackleEvent(tacklerId, _ballCarrierId, tacklePos));

            var endAbs = XToAbsoluteYard(tacklePos.X);

            // Update match state minimally: the play is effectively over at the tackle spot.
            _matchState.PlayNumber++;
            _matchState.SpotBallAbsoluteYard(endAbs);

            // Update play state (result + whistle reason).
            _playState.EndAbsoluteYard = endAbs;
            var startDist = PlayState.DistFromOwnGoal(_playState.StartAbsoluteYard, _matchState.OffenseDirection);
            var endDist = PlayState.DistFromOwnGoal(endAbs, _matchState.OffenseDirection);
            _playState.Result = _playState.Result with { YardsGained = endDist - startDist };
            _playState.WhistleReason = WhistleReason.Tackle;
            _playState.Phase = PlayPhase.PostPlay;
            _playState.BallState = BallState.Dead;
            _playState.BallOwnerEntityId = _ballCarrierId;
        }

        // Stop ball carrier
        if (_ballCarrierId != -1 && _velocityMapper.Has(_ballCarrierId))
        {
            _velocityMapper.Get(_ballCarrierId).Velocity = Vector2.Zero;
        }

        if (_velocityMapper.Has(tacklerId))
        {
            _velocityMapper.Get(tacklerId).Velocity = Vector2.Zero;
        }

        // Set behaviors
        var ballBehavior = _behaviorMapper.Get(_ballCarrierId);
        ballBehavior.State = BehaviorState.Idle;

        var tacklerBehavior = _behaviorMapper.Get(tacklerId);
        tacklerBehavior.State = BehaviorState.Idle;

        // Transition to end deterministically via PhaseTimer in UpdateTackle().
        // (Avoid Task.Delay here: it is nondeterministic and can fire mid-tick in headless runs.)
    }

    private void UpdateTackle()
    {
        // Short animation/pause, then end
        if (PhaseTimer > 2f)
        {
            CurrentPhase = GamePhase.End;
        }
    }

    private void ResetKickoff()
    {
        ApplyKickoffSetup(KickingTeam, ReceivingTeam);
    }

    private void ApplyKickoffSetup(int kickingTeam, int receivingTeam)
    {
        KickingTeam = kickingTeam;
        ReceivingTeam = receivingTeam;

        // Retag existing kickoff slice entities to the new teams so the slice can be reused.
        for (var i = 0; i < _kickingEntityIds.Count; i++)
        {
            var id = _kickingEntityIds[i];
            if (_teamMapper.Has(id))
            {
                var t = _teamMapper.Get(id);
                t.TeamIndex = KickingTeam;
                t.IsOffense = true;
                t.IsPlayerControlled = true;
            }
        }

        for (var i = 0; i < _receivingEntityIds.Count; i++)
        {
            var id = _receivingEntityIds[i];
            if (_teamMapper.Has(id))
            {
                var t = _teamMapper.Get(id);
                t.TeamIndex = ReceivingTeam;
                t.IsOffense = false;
                t.IsPlayerControlled = false;
            }
        }

        CurrentPhase = GamePhase.KickoffSetup;
        PhaseTimer = 0f;
        _ballKicked = false;
        _ballCaught = false;
        _tackleMade = false;

        // Keep the slice deterministic: reset high-level match view back to a kickoff snapshot.
        _matchState.ResetForKickoff(KickingTeam, ReceivingTeam);

        var startAbs = PlayState.ToAbsoluteYard(_matchState.BallSpot, _matchState.OffenseDirection);
        _playState.ResetForNewPlay(_matchState.PlayNumber + 1, startAbs);

        // Best-effort: ball starts held by the kicker in this slice.
        if (_kickerId != -1)
        {
            _playState.BallState = BallState.Held;
            _playState.BallOwnerEntityId = _kickerId;

            if (_ballMapper.Has(_kickerId))
                _ballMapper.Get(_kickerId).HasBall = true;
        }

        if (_ballCarrierId != -1 && _ballMapper.Has(_ballCarrierId))
            _ballMapper.Get(_ballCarrierId).HasBall = false;
    }

    private void SyncBallModelToEntity()
    {
        if (_ballEntityId == -1)
            return;
        if (!_ballStateMapper.Has(_ballEntityId) || !_ballOwnerMapper.Has(_ballEntityId))
            return;

        // Mirror the pure PlayState model onto the ECS components.
        // Motion itself is handled by BallPhysicsSystem.
        _ballStateMapper.Get(_ballEntityId).State = _playState.BallState;
        _ballOwnerMapper.Get(_ballEntityId).OwnerEntityId = _playState.BallOwnerEntityId;
    }

    private static int XToAbsoluteYard(float x)
    {
        // Keep this conversion local to the kickoff slice for now.
        // Rendering currently maps 0..100 yards into a virtual field width (see FieldRenderer).
        return TecmoSBGame.Field.FieldBounds.XToAbsoluteYard(x);
    }

    private static WhistleReason ParseWhistleReason(string? reason)
    {
        reason = (reason ?? string.Empty).Trim().ToLowerInvariant();

        // Allow namespaced reasons like "bounds:oob".
        var idx = reason.IndexOf(':');
        if (idx >= 0 && idx + 1 < reason.Length)
            reason = reason[(idx + 1)..];

        return reason switch
        {
            "tackle" => WhistleReason.Tackle,
            "oob" or "outofbounds" or "out_of_bounds" => WhistleReason.OutOfBounds,
            "td" or "touchdown" => WhistleReason.Touchdown,
            "safety" => WhistleReason.Safety,
            "touchback" or "tb" => WhistleReason.Touchback,
            "incomplete" => WhistleReason.Incomplete,
            "turnover" => WhistleReason.Turnover,
            "" => WhistleReason.Other,
            _ => WhistleReason.Other,
        };
    }

    public readonly record struct KickoffScenarioIds(
        int KickerId,
        int ReturnerId,
        int BallId,
        IReadOnlyList<int> AllEntityIds);

    // Called by MainGame/headless to spawn the kickoff scenario
    public KickoffScenarioIds SpawnKickoffScenario(World world)
    {
        _world = world;
        // Initialize match-level data for this slice.
        _matchState.ResetForKickoff(KickingTeam, ReceivingTeam);

        // Initialize play-level data.
        var startAbs = PlayState.ToAbsoluteYard(_matchState.BallSpot, _matchState.OffenseDirection);
        _playState.ResetForNewPlay(_matchState.PlayNumber + 1, startAbs);

        var all = new List<int>(capacity: 9);
        _kickingEntityIds.Clear();
        _receivingEntityIds.Clear();

        // Spawn the dedicated ball entity.
        _ballEntityId = BallEntityFactory.CreateBall(world, new Vector2(40, 112));
        all.Add(_ballEntityId);

        // Spawn kicking team.
        // Prefer YAML formation "00" (Kickoff) when available; fall back to the existing hard-coded slice.
        if (_formationData is not null && _formationSpawner is not null)
        {
            var formation = _formationSpawner.Spawn(
                world,
                _formationData,
                formationId: "00",
                teamIndex: KickingTeam,
                isOffense: true,
                playerControlled: true);

            foreach (var p in formation.Players)
            {
                all.Add(p.EntityId);
                _kickingEntityIds.Add(p.EntityId);
            }

            var kicker = formation.Players.FirstOrDefault(p => p.Role is PlayerRole.K or PlayerRole.P);
            _kickerId = kicker?.EntityId ?? -1;

            if (_kickerId <= 0 && formation.Players.Count > 0)
                _kickerId = formation.Players[0].EntityId;
        }
        else
        {
            // Spawn kicker (kicking team, player controlled)
            _kickerId = PlayerEntityFactory.CreateKicker(world, new Vector2(40, 112), KickingTeam, true);
            all.Add(_kickerId);
            _kickingEntityIds.Add(_kickerId);

            // Spawn coverage team (kicking team, AI)
            var c1 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 80), KickingTeam);
            var c2 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 144), KickingTeam);
            var c3 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(20, 112), KickingTeam);
            all.Add(c1);
            all.Add(c2);
            all.Add(c3);
            _kickingEntityIds.Add(c1);
            _kickingEntityIds.Add(c2);
            _kickingEntityIds.Add(c3);
        }

        // Spawn returner (receiving team, will be player controlled after catch)
        _ballCarrierId = PlayerEntityFactory.CreateReturner(world, new Vector2(200, 112), ReceivingTeam, false);
        all.Add(_ballCarrierId);
        _receivingEntityIds.Add(_ballCarrierId);

        // Kickoff starts with the kicker holding the ball (placeholder for tee/hand). Returner has no ball until caught.
        _playState.BallState = BallState.Held;
        _playState.BallOwnerEntityId = _kickerId;

        if (_ballMapper.Has(_kickerId))
            _ballMapper.Get(_kickerId).HasBall = true;
        if (_ballMapper.Has(_ballCarrierId))
            _ballMapper.Get(_ballCarrierId).HasBall = false;

        // Spawn blockers (receiving team, AI)
        var b1 = PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 80), ReceivingTeam);
        var b2 = PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 144), ReceivingTeam);
        var b3 = PlayerEntityFactory.CreateBlocker(world, new Vector2(220, 112), ReceivingTeam);
        all.Add(b1);
        all.Add(b2);
        all.Add(b3);
        _receivingEntityIds.Add(b1);
        _receivingEntityIds.Add(b2);
        _receivingEntityIds.Add(b3);

        return new KickoffScenarioIds(_kickerId, _ballCarrierId, _ballEntityId, all);
    }
}

public enum GamePhase
{
    KickoffSetup,   // Waiting for kick input
    KickoffFlight,  // Ball in air
    Return,         // Returner has ball
    Tackle,         // Tackle animation
    End             // Play over, waiting for restart
}
