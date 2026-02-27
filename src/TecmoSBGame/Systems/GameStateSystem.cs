using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Factories;
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
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<VelocityComponent> _velocityMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BehaviorComponent> _behaviorMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    private ComponentMapper<SpriteComponent> _spriteMapper;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.KickoffSetup;
    public float PhaseTimer { get; private set; } = 0f;
    public int KickingTeam { get; private set; } = 0;
    public int ReceivingTeam { get; private set; } = 1;

    public MatchState MatchState => _matchState;
    public PlayState PlayState => _playState;

    private int _ballCarrierId = -1;
    private int _kickerId = -1;
    private bool _ballKicked = false;
    private bool _ballCaught = false;
    private bool _tackleMade = false;

    public GameStateSystem(MatchState matchState, PlayState playState, GameEvents? events = null, bool headlessAutoAdvance = false)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _matchState = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _playState = playState ?? throw new ArgumentNullException(nameof(playState));
        _events = events;
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
    }

    public override void Update(GameTime gameTime)
    {
        // Poll events first (explicit determinism; one tick = one clear in the driver).
        _events?.Drain<WhistleEvent>(e =>
        {
            // Play is dead.
            CurrentPhase = GamePhase.End;
            PhaseTimer = 0f;

            // Record whistle reason/result in the play model (idempotent).
            if (_playState.WhistleReason == WhistleReason.None)
            {
                _playState.WhistleReason = ParseWhistleReason(e.Reason);
                _playState.Phase = PlayPhase.PostPlay;
                _playState.BallState = BallState.Dead;
            }
        });

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

        // Move the ball to kicked state
        if (_ballCarrierId != -1 && _ballMapper.Has(_ballCarrierId))
        {
            _ballMapper.Get(_ballCarrierId).HasBall = false;
        }

        // AI: Returners move to catch position
        foreach (var entityId in ActiveEntities)
        {
            if (!_teamMapper.Has(entityId))
                continue;

            var team = _teamMapper.Get(entityId);
            if (team.TeamIndex == ReceivingTeam)
            {
                var behavior = _behaviorMapper.Get(entityId);
                // Move toward predicted landing spot
                behavior.State = BehaviorState.MovingToPosition;
                behavior.TargetPosition = new Vector2(180, 112); // Approximate landing
            }
        }
    }

    private void UpdateKickoffFlight()
    {
        // Simulate ball flight
        if (PhaseTimer > 1.5f)
        {
            // Ball lands - check for catch
            CurrentPhase = GamePhase.Return;
            PhaseTimer = 0f;

            // Assign ball to returner
            foreach (var entityId in ActiveEntities)
            {
                if (!_teamMapper.Has(entityId))
                    continue;

                var team = _teamMapper.Get(entityId);
                if (team.TeamIndex == ReceivingTeam)
                {
                    // Give ball to this returner
                    if (!_ballMapper.Has(entityId))
                    {
                        // This shouldn't happen for returner, but handle it
                        continue;
                    }

                    _ballCarrierId = entityId;
                    _ballMapper.Get(entityId).HasBall = true;
                    team.IsOffense = true;

                    _playState.BallState = BallState.Held;
                    _playState.BallOwnerEntityId = entityId;

                    _events?.Publish(new BallCaughtEvent(entityId, _positionMapper.Get(entityId).Position));

                    // Set player control
                    team.IsPlayerControlled = true;

                    var behavior = _behaviorMapper.Get(entityId);
                    behavior.State = BehaviorState.Idle;

                    break;
                }
            }

            // Coverage team now pursues
            foreach (var entityId in ActiveEntities)
            {
                if (!_teamMapper.Has(entityId))
                    continue;

                var team = _teamMapper.Get(entityId);
                if (team.TeamIndex == KickingTeam)
                {
                    var behavior = _behaviorMapper.Get(entityId);
                    behavior.State = BehaviorState.TrackingPlayer;
                    behavior.TargetEntityId = _ballCarrierId;
                }
            }
        }
    }

    private void UpdateReturn()
    {
        if (_ballCarrierId == -1)
            return;

        var ballPos = _positionMapper.Get(_ballCarrierId).Position;

        // Check for tackle (simple distance check)
        foreach (var entityId in ActiveEntities)
        {
            if (entityId == _ballCarrierId)
                continue;

            if (!_teamMapper.Has(entityId))
                continue;

            var team = _teamMapper.Get(entityId);
            if (team.TeamIndex == KickingTeam) // Defensive team
            {
                var defenderPos = _positionMapper.Get(entityId).Position;
                float dist = Vector2.Distance(ballPos, defenderPos);

                if (dist < 10f) // Tackle range
                {
                    ExecuteTackle(entityId);
                    return;
                }
            }
        }
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
        CurrentPhase = GamePhase.KickoffSetup;
        PhaseTimer = 0f;
        _ballKicked = false;
        _ballCaught = false;
        _tackleMade = false;

        // Keep the slice deterministic: reset high-level match view back to a kickoff snapshot.
        _matchState.ResetForKickoff(KickingTeam, ReceivingTeam);

        var startAbs = PlayState.ToAbsoluteYard(_matchState.BallSpot, _matchState.OffenseDirection);
        _playState.ResetForNewPlay(_matchState.PlayNumber + 1, startAbs);

        // Best-effort: ball starts with the current returner placeholder in this slice.
        if (_ballCarrierId != -1)
        {
            _playState.BallState = BallState.Held;
            _playState.BallOwnerEntityId = _ballCarrierId;
        }

        // Reset positions (simplified - would respawn entities)
        // For now, just reset phases
    }

    private static int XToAbsoluteYard(float x)
    {
        // Keep this conversion local to the kickoff slice for now.
        // Rendering currently maps 0..100 yards into a virtual field width (see FieldRenderer).
        const float fieldLeft = 16f;
        const float fieldRight = 240f;

        var t = (x - fieldLeft) / (fieldRight - fieldLeft);
        var yard = (int)MathF.Round(t * 100f);
        return Math.Clamp(yard, 0, 100);
    }

    private static WhistleReason ParseWhistleReason(string? reason)
    {
        reason = (reason ?? string.Empty).Trim().ToLowerInvariant();
        return reason switch
        {
            "tackle" => WhistleReason.Tackle,
            "oob" or "outofbounds" or "out_of_bounds" => WhistleReason.OutOfBounds,
            "td" or "touchdown" => WhistleReason.Touchdown,
            "safety" => WhistleReason.Safety,
            "incomplete" => WhistleReason.Incomplete,
            "turnover" => WhistleReason.Turnover,
            "" => WhistleReason.Other,
            _ => WhistleReason.Other,
        };
    }

    public readonly record struct KickoffScenarioIds(
        int KickerId,
        int ReturnerId,
        IReadOnlyList<int> AllEntityIds);

    // Called by MainGame/headless to spawn the kickoff scenario
    public KickoffScenarioIds SpawnKickoffScenario(World world)
    {
        // Initialize match-level data for this slice.
        _matchState.ResetForKickoff(KickingTeam, ReceivingTeam);

        // Initialize play-level data.
        var startAbs = PlayState.ToAbsoluteYard(_matchState.BallSpot, _matchState.OffenseDirection);
        _playState.ResetForNewPlay(_matchState.PlayNumber + 1, startAbs);

        var all = new List<int>(capacity: 8);

        // Spawn kicker (kicking team, player controlled)
        _kickerId = PlayerEntityFactory.CreateKicker(world, new Vector2(40, 112), KickingTeam, true);
        all.Add(_kickerId);

        // Spawn coverage team (kicking team, AI)
        all.Add(PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 80), KickingTeam));
        all.Add(PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 144), KickingTeam));
        all.Add(PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(20, 112), KickingTeam));

        // Spawn returner (receiving team, will be player controlled after catch)
        _ballCarrierId = PlayerEntityFactory.CreateReturner(world, new Vector2(200, 112), ReceivingTeam, false);
        all.Add(_ballCarrierId);

        _playState.BallState = BallState.Held;
        _playState.BallOwnerEntityId = _ballCarrierId;

        // Spawn blockers (receiving team, AI)
        all.Add(PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 80), ReceivingTeam));
        all.Add(PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 144), ReceivingTeam));
        all.Add(PlayerEntityFactory.CreateBlocker(world, new Vector2(220, 112), ReceivingTeam));

        return new KickoffScenarioIds(_kickerId, _ballCarrierId, all);
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
