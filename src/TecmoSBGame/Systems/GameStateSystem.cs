using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Factories;

namespace TecmoSBGame.Systems;

/// <summary>
/// Manages game state for a single playable slice (kickoff scenario).
/// Handles kickoff setup, receiving, and tackle resolution.
/// </summary>
public class GameStateSystem : EntityUpdateSystem
{
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

    private int _ballCarrierId = -1;
    private int _kickerId = -1;
    private bool _ballKicked = false;
    private bool _ballCaught = false;
    private bool _tackleMade = false;

    public GameStateSystem() : base(Aspect.All(typeof(PositionComponent)))
    {
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
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        PhaseTimer += dt;

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
                // Wait for restart input
                if (Keyboard.GetState().IsKeyDown(Keys.Enter))
                {
                    ResetKickoff();
                }
                break;
        }
    }

    private void UpdateKickoffSetup()
    {
        // Wait for user to press space to kick
        if (Keyboard.GetState().IsKeyDown(Keys.Space))
        {
            ExecuteKickoff();
        }
    }

    private void ExecuteKickoff()
    {
        CurrentPhase = GamePhase.KickoffFlight;
        PhaseTimer = 0f;
        _ballKicked = true;

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

        // Transition to end after delay
        Task.Delay(2000).ContinueWith(_ => CurrentPhase = GamePhase.End);
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

        // Reset positions (simplified - would respawn entities)
        // For now, just reset phases
    }

    // Called by MainGame to spawn the kickoff scenario
    public void SpawnKickoffScenario(World world)
    {
        // Spawn kicker (kicking team, player controlled)
        _kickerId = PlayerEntityFactory.CreateKicker(world, new Vector2(40, 112), KickingTeam, true);

        // Spawn coverage team (kicking team, AI)
        PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 80), KickingTeam);
        PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(30, 144), KickingTeam);
        PlayerEntityFactory.CreateCoveragePlayer(world, new Vector2(20, 112), KickingTeam);

        // Spawn returner (receiving team, will be player controlled after catch)
        _ballCarrierId = PlayerEntityFactory.CreateReturner(world, new Vector2(200, 112), ReceivingTeam, false);

        // Spawn blockers (receiving team, AI)
        PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 80), ReceivingTeam);
        PlayerEntityFactory.CreateBlocker(world, new Vector2(210, 144), ReceivingTeam);
        PlayerEntityFactory.CreateBlocker(world, new Vector2(220, 112), ReceivingTeam);
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
