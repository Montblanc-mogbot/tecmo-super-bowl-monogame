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
public partial class GameStateSystem : EntityUpdateSystem
{
    private readonly bool _headlessAutoAdvance;
    private readonly GameEvents? _events;
    private readonly MatchState _matchState;
    private readonly PlayState _playState;
    private readonly FormationDataConfig? _formationData;
    private readonly FormationSpawner? _formationSpawner;
    private readonly PlayListConfig? _playList;
    private readonly DefensePlayConfig? _defensePlays;
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<VelocityComponent> _velocityMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BehaviorComponent> _behaviorMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    private ComponentMapper<SpriteComponent> _spriteMapper;
    private ComponentMapper<BallComponent> _ball;

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

    // Mode: kickoff slice vs scrimmage mode.
    private bool _kickoffActive = true;

    public GameStateSystem(
        MatchState matchState,
        PlayState playState,
        GameEvents? events = null,
        FormationDataConfig? formationData = null,
        FormationSpawner? formationSpawner = null,
        PlayListConfig? playList = null,
        DefensePlayConfig? defensePlays = null,
        bool headlessAutoAdvance = false)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _matchState = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _playState = playState ?? throw new ArgumentNullException(nameof(playState));
        _events = events;
        _formationData = formationData;
        _formationSpawner = formationSpawner;
        _playList = playList;
        _defensePlays = defensePlays;
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
        _ball = mapperService.GetMapper<BallComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Once we leave the kickoff slice, this system becomes inert.
        // Scrimmage is handled by loop + playcall + play execution systems.
        if (_kickoffActive && _playState.AllowPass)
            _kickoffActive = false;

        if (_events is not null)
        {
            // React to score->kickoff transitions.
            // (Use Read() so multiple systems can observe this setup event.)
            var setups = _events.Read<KickoffSetupEvent>();
            if (setups.Count > 0)
            {
                var k = setups[^1];
                ApplyKickoffSetup(k.KickingTeam, k.ReceivingTeam);
            }
        }

        if (!_kickoffActive)
            return;

        // Do not Drain whistles here: LoopMachineSystem + PlayEndSystem rely on observing them.
        // Instead, let PlayEndSystem finalize PlayState/MatchState.
        if (_playState.IsOver)
        {
            CurrentPhase = GamePhase.End;
            PhaseTimer = 0f;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        PhaseTimer += dt;

        // Keep PlayState updated for deterministic headless snapshots (kickoff slice only).
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

            // Overwrite flight model for deterministic parametric motion.
            if (_ball.Has(_ballEntityId))
            {
                var f = _ball.Get(_ballEntityId);
                f.FlightKind = BallFlightKind.Kickoff;
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

                // Kickoff is immediately "in air".
                f.State = BallState.InAir;
                f.OwnerEntityId = null;
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
        if (_ballEntityId == -1 || !_ball.Has(_ballEntityId))
            return;

        var flight = _ball.Get(_ballEntityId);
        if (flight.FlightKind != BallFlightKind.Kickoff || !flight.IsComplete)
            return;

        // Ball lands - check for catch.
        CurrentPhase = GamePhase.Return;
        PhaseTimer = 0f;

        // Assign ball to the best candidate returner.
        // Tecmo uses a specific returner slot, but for now pick deterministically:
        // choose the receiving player (with BallCarrierComponent) closest to the ball landing position.
        var landPos = _positionMapper.Get(_ballEntityId).Position;

        int chosenId = -1;
        float chosenDistSq = float.PositiveInfinity;

        for (var i = 0; i < _receivingEntityIds.Count; i++)
        {
            var entityId = _receivingEntityIds[i];
            if (!_teamMapper.Has(entityId) || !_ballMapper.Has(entityId) || !_positionMapper.Has(entityId))
                continue;

            var d = _positionMapper.Get(entityId).Position - landPos;
            var distSq = d.LengthSquared();
            if (distSq < chosenDistSq)
            {
                chosenDistSq = distSq;
                chosenId = entityId;
            }
        }

        if (chosenId != -1)
        {
            var team = _teamMapper.Get(chosenId);

            // Clear any prior "has ball" flags on receiving unit.
            for (var i = 0; i < _receivingEntityIds.Count; i++)
            {
                var id = _receivingEntityIds[i];
                if (_ballMapper.Has(id))
                    _ballMapper.Get(id).HasBall = false;
            }

            _ballCarrierId = chosenId;
            _ballMapper.Get(chosenId).HasBall = true;
            team.IsOffense = true;

            _playState.BallState = BallState.Held;
            _playState.BallOwnerEntityId = chosenId;

            _events?.Publish(new BallCaughtEvent(chosenId, _positionMapper.Get(chosenId).Position));

            // Set player control to the returner only.
            team.IsPlayerControlled = true;

            if (_behaviorMapper.Has(chosenId))
            {
                var behavior = _behaviorMapper.Get(chosenId);
                behavior.State = BehaviorState.Idle;
            }
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
        _kickoffActive = true;
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
                t.IsOffense = false; // kickoff coverage team = defense for tackle logic
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
                t.IsOffense = true; // return unit = offense for tackle logic
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
        _playState.AllowPass = false; // kickoff slice: no passing

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
        if (!_ball.Has(_ballEntityId))
            return;

        // Mirror the pure PlayState model onto the ECS component.
        // Motion itself is handled by BallPhysicsSystem.
        var b = _ball.Get(_ballEntityId);
        b.State = _playState.BallState;
        b.OwnerEntityId = _playState.BallOwnerEntityId;
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
        _kickoffActive = true;

        // Initialize match-level data for this slice.
        _matchState.ResetForKickoff(KickingTeam, ReceivingTeam);

        // Initialize play-level data.
        var startAbs = PlayState.ToAbsoluteYard(_matchState.BallSpot, _matchState.OffenseDirection);
        _playState.ResetForNewPlay(_matchState.PlayNumber + 1, startAbs);
        _playState.AllowPass = false; // kickoff slice: no passing

        var all = new List<int>(capacity: 9);
        _kickingEntityIds.Clear();
        _receivingEntityIds.Clear();

        // Spawn the dedicated ball entity.
        _ballEntityId = BallEntityFactory.CreateBall(world, new Vector2(40, 112));
        all.Add(_ballEntityId);

        // Tecmo kickoff structure:
        // - Receiving team loads the kickoff-return offensive formation.
        // - Kicking team runs a kickoff defense play.
        //
        // Our YAML currently only encodes the offensive formation script (formation id "00"),
        // so we apply it to the *receiving* team to get authentic return-unit movement.
        // The kicking team still uses the existing hard-coded slice until defensive kickoff
        // play scripts are also mapped into ECS.

        // Spawn receiving team from YAML kickoff-return formation when available.
        if (_formationData is not null && _formationSpawner is not null)
        {
            var recvFormation = _formationSpawner.Spawn(
                world,
                _formationData,
                formationId: "00",
                teamIndex: ReceivingTeam,
                isOffense: true,
                playerControlled: false);

            Console.WriteLine($"[kickoff] spawned receiving formation 00 team={ReceivingTeam} players={recvFormation.Players.Count}");

            foreach (var p in recvFormation.Players)
            {
                all.Add(p.EntityId);
                _receivingEntityIds.Add(p.EntityId);

                if (world.GetEntity(p.EntityId).Has<FormationScriptComponent>())
                {
                    var sc = world.GetEntity(p.EntityId).Get<FormationScriptComponent>();
                    if (sc.Ops.Count > 0)
                        Console.WriteLine($"  [kickoff] script slot={p.Slot} role={p.Role} id={p.EntityId} ops={sc.Ops.Count} first={sc.Ops[0].Kind}:{sc.Ops[0].Raw}");
                    else
                        Console.WriteLine($"  [kickoff] script slot={p.Slot} role={p.Role} id={p.EntityId} ops=0");
                }
                else
                {
                    Console.WriteLine($"  [kickoff] NO SCRIPT slot={p.Slot} role={p.Role} id={p.EntityId}");
                }
            }

            // Prefer KR from the formation if present.
            var kr = recvFormation.Players.FirstOrDefault(p => p.Role is PlayerRole.RB or PlayerRole.WR);
            _ballCarrierId = kr.EntityId;
        }
        else
        {
            // Fallback: spawn returner + a few blockers.
            _ballCarrierId = PlayerEntityFactory.CreateReturner(world, new Vector2(200, 112), ReceivingTeam, false);
            all.Add(_ballCarrierId);
            _receivingEntityIds.Add(_ballCarrierId);

            var b1 = PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 80), ReceivingTeam);
            var b2 = PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 144), ReceivingTeam);
            var b3 = PlayerEntityFactory.CreateBlocker(world, new Vector2(220, 112), ReceivingTeam);
            all.Add(b1);
            all.Add(b2);
            all.Add(b3);
            _receivingEntityIds.Add(b1);
            _receivingEntityIds.Add(b2);
            _receivingEntityIds.Add(b3);
        }

        // Spawn kicking team (still hard-coded slice for now).
        _kickerId = PlayerEntityFactory.CreateKicker(world, new Vector2(40, 112), KickingTeam, true);
        all.Add(_kickerId);
        _kickingEntityIds.Add(_kickerId);

        var c1 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 80), KickingTeam);
        var c2 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 144), KickingTeam);
        var c3 = PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(20, 112), KickingTeam);
        all.Add(c1);
        all.Add(c2);
        all.Add(c3);
        _kickingEntityIds.Add(c1);
        _kickingEntityIds.Add(c2);
        _kickingEntityIds.Add(c3);

        // Kickoff starts with the kicker holding the ball (placeholder for tee/hand). Returner has no ball until caught.
        _playState.BallState = BallState.Held;
        _playState.BallOwnerEntityId = _kickerId;

        if (_ballMapper.Has(_kickerId))
            _ballMapper.Get(_kickerId).HasBall = true;
        if (_ballMapper.Has(_ballCarrierId))
            _ballMapper.Get(_ballCarrierId).HasBall = false;

        // NOTE: Receiving-unit blockers are spawned either via YAML formation (preferred)
        // or via fallback hard-coded slice above.

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
