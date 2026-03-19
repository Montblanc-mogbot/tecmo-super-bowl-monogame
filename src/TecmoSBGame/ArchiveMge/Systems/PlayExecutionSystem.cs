using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Executes plays by assigning behaviors to players based on their roles.
/// Converts play calls (routes, blocks, rushes) into actionable AI behaviors.
/// </summary>
public class PlayExecutionSystem : EntityUpdateSystem
{
    private ComponentMapper<BehaviorComponent> _behaviorMapper;
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;

    // Play state
    public bool IsPlayActive { get; private set; } = false;
    public PlayType CurrentPlay { get; private set; } = PlayType.None;
    public float PlayTime { get; private set; } = 0f;

    public PlayExecutionSystem() : base(Aspect.All(typeof(BehaviorComponent), typeof(TeamComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!IsPlayActive)
            return;

        PlayTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update behaviors based on play progression
        foreach (var entityId in ActiveEntities)
        {
            UpdatePlayerBehavior(entityId);
        }
    }

    /// <summary>
    /// Starts a new play with the given play type.
    /// </summary>
    public void StartPlay(PlayType playType, int offenseTeam, int defenseTeam)
    {
        CurrentPlay = playType;
        IsPlayActive = true;
        PlayTime = 0f;

        // Assign initial behaviors based on play type
        foreach (var entityId in ActiveEntities)
        {
            var team = _teamMapper.Get(entityId);
            
            if (team.TeamIndex == offenseTeam)
            {
                AssignOffensiveBehavior(entityId, playType);
            }
            else if (team.TeamIndex == defenseTeam)
            {
                AssignDefensiveBehavior(entityId, playType);
            }
        }
    }

    /// <summary>
    /// Ends the current play.
    /// </summary>
    public void EndPlay()
    {
        IsPlayActive = false;
        CurrentPlay = PlayType.None;

        // Reset all behaviors to idle
        foreach (var entityId in ActiveEntities)
        {
            var behavior = _behaviorMapper.Get(entityId);
            behavior.State = BehaviorState.Idle;
        }
    }

    private void UpdatePlayerBehavior(int entityId)
    {
        var behavior = _behaviorMapper.Get(entityId);
        var position = _positionMapper.Get(entityId);

        switch (behavior.State)
        {
            case BehaviorState.RunningRoute:
                UpdateRouteBehavior(entityId, behavior, position);
                break;

            case BehaviorState.Blocking:
                UpdateBlockingBehavior(entityId, behavior, position);
                break;

            case BehaviorState.RushingQB:
                UpdateRushBehavior(entityId, behavior, position);
                break;

            case BehaviorState.TrackingPlayer:
                UpdateTrackingBehavior(entityId, behavior, position);
                break;

            case BehaviorState.Tackling:
                UpdateTackleBehavior(entityId, behavior, position);
                break;
        }
    }

    private void AssignOffensiveBehavior(int entityId, PlayType playType)
    {
        var behavior = _behaviorMapper.Get(entityId);
        var hasBall = _ballMapper.Has(entityId) && _ballMapper.Get(entityId).HasBall;

        if (hasBall)
        {
            // Ball carrier - handle based on play
            switch (playType)
            {
                case PlayType.Run:
                    behavior.State = BehaviorState.MovingToPosition;
                    behavior.TargetPosition = CalculateRunTarget(entityId);
                    break;

                case PlayType.Pass:
                    // QB drops back initially
                    behavior.State = BehaviorState.MovingToPosition;
                    behavior.TargetPosition = CalculateDropbackTarget(entityId);
                    break;
            }
        }
        else
        {
            // Non-ball carriers
            string position = GetPlayerPosition(entityId);

            if (IsOffensiveLineman(position))
            {
                behavior.State = BehaviorState.Blocking;
                behavior.TargetEntityId = FindNearestDefender(entityId);
            }
            else if (IsReceiver(position))
            {
                behavior.State = BehaviorState.RunningRoute;
                behavior.TargetPosition = CalculateRouteTarget(entityId, playType);
            }
            else
            {
                behavior.State = BehaviorState.MovingToPosition;
                behavior.TargetPosition = _positionMapper.Get(entityId).Position;
            }
        }
    }

    private void AssignDefensiveBehavior(int entityId, PlayType playType)
    {
        var behavior = _behaviorMapper.Get(entityId);
        string position = GetPlayerPosition(entityId);

        if (IsDefensiveLineman(position))
        {
            // D-line rushes the QB
            behavior.State = BehaviorState.RushingQB;
            behavior.TargetEntityId = FindBallCarrier();
        }
        else if (IsLinebacker(position))
        {
            // LBs read and react
            behavior.State = BehaviorState.TrackingPlayer;
            behavior.TargetEntityId = FindBallCarrier();
        }
        else if (IsDefensiveBack(position))
        {
            // DBs cover receivers
            behavior.State = BehaviorState.TrackingPlayer;
            behavior.TargetEntityId = FindNearestReceiver(entityId);
        }
    }

    private void UpdateRouteBehavior(int entityId, BehaviorComponent behavior, PositionComponent position)
    {
        // Check if route is complete
        float distToTarget = Vector2.Distance(position.Position, behavior.TargetPosition);
        
        if (distToTarget < 5f)
        {
            // Route complete - turn and look for ball
            behavior.State = BehaviorState.Idle;
        }
    }

    private void UpdateBlockingBehavior(int entityId, BehaviorComponent behavior, PositionComponent position)
    {
        // Stay between ball carrier and defender
        int ballCarrierId = FindBallCarrier();
        if (ballCarrierId == -1)
            return;

        var ballPos = _positionMapper.Get(ballCarrierId).Position;
        var defenderPos = _positionMapper.Get(behavior.TargetEntityId).Position;

        // Position between ball and defender
        Vector2 blockPosition = ballPos + (defenderPos - ballPos) * 0.5f;
        behavior.TargetPosition = blockPosition;
        behavior.State = BehaviorState.MovingToPosition;
    }

    private void UpdateRushBehavior(int entityId, BehaviorComponent behavior, PositionComponent position)
    {
        // Continue rushing the ball carrier/QB
        int targetId = behavior.TargetEntityId;
        if (targetId == -1 || !_positionMapper.Has(targetId))
        {
            targetId = FindBallCarrier();
            behavior.TargetEntityId = targetId;
        }

        if (targetId != -1)
        {
            behavior.TargetPosition = _positionMapper.Get(targetId).Position;
        }
    }

    private void UpdateTrackingBehavior(int entityId, BehaviorComponent behavior, PositionComponent position)
    {
        // Maintain coverage/tracking distance
        int targetId = behavior.TargetEntityId;
        if (targetId == -1 || !_positionMapper.Has(targetId))
            return;

        var targetPos = _positionMapper.Get(targetId).Position;
        float dist = Vector2.Distance(position.Position, targetPos);

        // Adjust position based on distance
        if (dist > 15f)
        {
            // Too far - close in
            behavior.TargetPosition = targetPos;
            behavior.State = BehaviorState.MovingToPosition;
        }
        else if (dist < 8f)
        {
            // Close enough - attempt tackle if ball carrier
            if (_ballMapper.Has(targetId) && _ballMapper.Get(targetId).HasBall)
            {
                behavior.State = BehaviorState.Tackling;
            }
        }
    }

    private void UpdateTackleBehavior(int entityId, BehaviorComponent behavior, PositionComponent position)
    {
        int targetId = behavior.TargetEntityId;
        if (targetId == -1 || !_positionMapper.Has(targetId))
            return;

        var targetPos = _positionMapper.Get(targetId).Position;
        float dist = Vector2.Distance(position.Position, targetPos);

        if (dist < 5f)
        {
            // Tackle made!
            OnTackleMade(entityId, targetId);
        }
        else
        {
            // Keep pursuing
            behavior.TargetPosition = targetPos;
        }
    }

    private void OnTackleMade(int tacklerId, int ballCarrierId)
    {
        // End the play
        EndPlay();

        // Signal to game state system
        PlayEnded?.Invoke(this, new PlayEndedEventArgs 
        { 
            TacklerId = tacklerId, 
            BallCarrierId = ballCarrierId,
            PlayType = CurrentPlay,
            PlayDuration = PlayTime
        });
    }

    // Helper methods

    private Vector2 CalculateRunTarget(int entityId)
    {
        // Simple: run toward end zone
        var pos = _positionMapper.Get(entityId).Position;
        return new Vector2(pos.X + 100, pos.Y); // Run right
    }

    private Vector2 CalculateDropbackTarget(int entityId)
    {
        // QB drops back 10 yards
        var pos = _positionMapper.Get(entityId).Position;
        return new Vector2(pos.X - 10, pos.Y);
    }

    private Vector2 CalculateRouteTarget(int entityId, PlayType playType)
    {
        var pos = _positionMapper.Get(entityId).Position;
        
        // Simple route patterns
        string receiverPos = GetPlayerPosition(entityId);
        
        return receiverPos switch
        {
            "WR1" => new Vector2(pos.X + 60, pos.Y - 40), // Out left
            "WR2" => new Vector2(pos.X + 60, pos.Y + 40), // Out right
            "TE" => new Vector2(pos.X + 30, pos.Y + 20),  // Short out
            _ => new Vector2(pos.X + 50, pos.Y)           // Straight
        };
    }

    private int FindBallCarrier()
    {
        foreach (var entityId in ActiveEntities)
        {
            if (_ballMapper.Has(entityId) && _ballMapper.Get(entityId).HasBall)
                return entityId;
        }
        return -1;
    }

    private int FindNearestDefender(int entityId)
    {
        var pos = _positionMapper.Get(entityId).Position;
        var team = _teamMapper.Get(entityId);
        
        int nearest = -1;
        float nearestDist = float.MaxValue;

        foreach (var otherId in ActiveEntities)
        {
            if (otherId == entityId)
                continue;

            var otherTeam = _teamMapper.Get(otherId);
            if (otherTeam.TeamIndex == team.TeamIndex)
                continue; // Same team

            float dist = Vector2.Distance(pos, _positionMapper.Get(otherId).Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = otherId;
            }
        }

        return nearest;
    }

    private int FindNearestReceiver(int entityId)
    {
        var pos = _positionMapper.Get(entityId).Position;
        var team = _teamMapper.Get(entityId);
        
        int nearest = -1;
        float nearestDist = float.MaxValue;

        foreach (var otherId in ActiveEntities)
        {
            if (otherId == entityId)
                continue;

            var otherTeam = _teamMapper.Get(otherId);
            if (otherTeam.TeamIndex == team.TeamIndex)
                continue; // Same team

            if (!IsReceiver(GetPlayerPosition(otherId)))
                continue;

            float dist = Vector2.Distance(pos, _positionMapper.Get(otherId).Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = otherId;
            }
        }

        return nearest;
    }

    private string GetPlayerPosition(int entityId)
    {
        // This would come from PlayerAttributesComponent
        // For now, return empty string
        return "";
    }

    private bool IsOffensiveLineman(string position)
    {
        return position is "C" or "G" or "T" or "LG" or "RG" or "LT" or "RT" or "OL";
    }

    private bool IsReceiver(string position)
    {
        return position is "WR" or "WR1" or "WR2" or "WR3" or "TE" or "RB" or "FB";
    }

    private bool IsDefensiveLineman(string position)
    {
        return position is "DE" or "DT" or "NT" or "DL";
    }

    private bool IsLinebacker(string position)
    {
        return position is "LB" or "MLB" or "OLB" or "ILB";
    }

    private bool IsDefensiveBack(string position)
    {
        return position is "CB" or "S" or "FS" or "SS" or "DB";
    }

    // Events
    public event EventHandler<PlayEndedEventArgs>? PlayEnded;
}

/// <summary>
/// Types of offensive plays.
/// </summary>
public enum PlayType
{
    None,
    Run,
    Pass,
    Screen,
    Draw,
    PlayAction,
    SpecialTeams
}

/// <summary>
/// Event args for when a play ends.
/// </summary>
public class PlayEndedEventArgs : EventArgs
{
    public int TacklerId { get; set; }
    public int BallCarrierId { get; set; }
    public PlayType PlayType { get; set; }
    public float PlayDuration { get; set; }
    public int YardsGained { get; set; }
    public bool Touchdown { get; set; }
}
