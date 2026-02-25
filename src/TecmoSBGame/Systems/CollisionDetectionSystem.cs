using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Handles collision detection between entities (players, ball, field boundaries).
/// </summary>
public class CollisionDetectionSystem : EntityUpdateSystem
{
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    
    // Collision configuration
    public const float PLAYER_RADIUS = 6f;  // Player collision circle radius
    public const float TACKLE_DISTANCE = 8f; // Distance for tackle to occur
    public const float INTERACTION_DISTANCE = 12f; // Distance for interactions (blocking, etc.)
    
    // Events
    public event Action<int, int>? OnTackle; // tacklerId, ballCarrierId
    public event Action<int, int>? OnBlock;  // blockerId, defenderId
    public event Action<int>? OnOutOfBounds; // entityId
    
    // Field boundaries (NES 256x224 coordinates)
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    public CollisionDetectionSystem() : base(Aspect.All(typeof(PositionComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var activeEntities = ActiveEntities.ToList();
        
        // Check each entity against others
        for (int i = 0; i < activeEntities.Count; i++)
        {
            var entityA = activeEntities[i];
            var posA = _positionMapper.Get(entityA);
            
            // Check boundary collision
            CheckBoundaryCollision(entityA, posA);
            
            // Check against other entities
            for (int j = i + 1; j < activeEntities.Count; j++)
            {
                var entityB = activeEntities[j];
                CheckEntityCollision(entityA, entityB);
            }
        }
    }
    
    private void CheckBoundaryCollision(int entityId, PositionComponent position)
    {
        var pos = position.Position;
        bool outOfBounds = false;
        
        // Check sideline bounds
        if (pos.X < FIELD_LEFT || pos.X > FIELD_RIGHT)
        {
            outOfBounds = true;
        }
        
        // Check endzone bounds (slightly different for gameplay)
        if (pos.Y < FIELD_TOP || pos.Y > FIELD_BOTTOM)
        {
            outOfBounds = true;
        }
        
        if (outOfBounds)
        {
            // Clamp position
            pos.X = MathHelper.Clamp(pos.X, FIELD_LEFT, FIELD_RIGHT);
            pos.Y = MathHelper.Clamp(pos.Y, FIELD_TOP, FIELD_BOTTOM);
            position.Position = pos;
            
            OnOutOfBounds?.Invoke(entityId);
        }
    }
    
    private void CheckEntityCollision(int entityA, int entityB)
    {
        var posA = _positionMapper.Get(entityA).Position;
        var posB = _positionMapper.Get(entityB).Position;
        
        float distance = Vector2.Distance(posA, posB);
        
        // Check if they're on the same team
        bool sameTeam = AreSameTeam(entityA, entityB);
        
        // Check for ball carrier
        bool aHasBall = _ballMapper.Has(entityA) && _ballMapper.Get(entityA).HasBall;
        bool bHasBall = _ballMapper.Has(entityB) && _ballMapper.Get(entityB).HasBall;
        
        // Tackle detection: defender hits ball carrier
        if (!sameTeam && distance <= TACKLE_DISTANCE)
        {
            if (aHasBall)
            {
                OnTackle?.Invoke(entityB, entityA); // B tackles A
            }
            else if (bHasBall)
            {
                OnTackle?.Invoke(entityA, entityB); // A tackles B
            }
        }
        
        // Block detection: offensive player blocks defender
        if (!sameTeam && distance <= INTERACTION_DISTANCE)
        {
            var teamA = _teamMapper.Get(entityA);
            var teamB = _teamMapper.Get(entityB);
            
            // Only block if one is offense and other is defense
            if (teamA.IsOffense && !teamB.IsOffense)
            {
                OnBlock?.Invoke(entityA, entityB);
            }
            else if (!teamA.IsOffense && teamB.IsOffense)
            {
                OnBlock?.Invoke(entityB, entityA);
            }
        }
        
        // Simple collision resolution (push apart)
        if (distance < PLAYER_RADIUS * 2 && distance > 0)
        {
            ResolveCollision(entityA, entityB, posA, posB, distance);
        }
    }
    
    private void ResolveCollision(int entityA, int entityB, Vector2 posA, Vector2 posB, float distance)
    {
        // Calculate push direction
        Vector2 direction = posB - posA;
        direction.Normalize();
        
        // Push apart by half the overlap distance
        float overlap = (PLAYER_RADIUS * 2) - distance;
        Vector2 push = direction * (overlap * 0.5f);
        
        // Apply to positions
        var positionA = _positionMapper.Get(entityA);
        var positionB = _positionMapper.Get(entityB);
        
        positionA.Position -= push;
        positionB.Position += push;
    }
    
    private bool AreSameTeam(int entityA, int entityB)
    {
        if (!_teamMapper.Has(entityA) || !_teamMapper.Has(entityB))
            return false;
        
        return _teamMapper.Get(entityA).TeamIndex == _teamMapper.Get(entityB).TeamIndex;
    }
    
    /// <summary>
    /// Gets all entities within a certain radius of a position.
    /// </summary>
    public List<int> GetEntitiesInRadius(Vector2 position, float radius)
    {
        var result = new List<int>();
        
        foreach (var entityId in ActiveEntities)
        {
            var entityPos = _positionMapper.Get(entityId).Position;
            if (Vector2.Distance(position, entityPos) <= radius)
            {
                result.Add(entityId);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Gets the nearest entity of a specific team.
    /// </summary>
    public int GetNearestOpponent(int entityId, float maxDistance = float.MaxValue)
    {
        if (!_teamMapper.Has(entityId))
            return -1;
        
        var myTeam = _teamMapper.Get(entityId).TeamIndex;
        var myPos = _positionMapper.Get(entityId).Position;
        
        int nearest = -1;
        float nearestDist = maxDistance;
        
        foreach (var otherId in ActiveEntities)
        {
            if (otherId == entityId)
                continue;
            
            if (!_teamMapper.Has(otherId))
                continue;
            
            var otherTeam = _teamMapper.Get(otherId).TeamIndex;
            if (otherTeam == myTeam)
                continue; // Same team
            
            var dist = Vector2.Distance(myPos, _positionMapper.Get(otherId).Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = otherId;
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Checks if a position is within the field boundaries.
    /// </summary>
    public bool IsInBounds(Vector2 position)
    {
        return position.X >= FIELD_LEFT && position.X <= FIELD_RIGHT &&
               position.Y >= FIELD_TOP && position.Y <= FIELD_BOTTOM;
    }
    
    /// <summary>
    /// Gets the nearest point within field boundaries.
    /// </summary>
    public Vector2 ClampToBounds(Vector2 position)
    {
        return new Vector2(
            MathHelper.Clamp(position.X, FIELD_LEFT, FIELD_RIGHT),
            MathHelper.Clamp(position.Y, FIELD_TOP, FIELD_BOTTOM)
        );
    }
}
