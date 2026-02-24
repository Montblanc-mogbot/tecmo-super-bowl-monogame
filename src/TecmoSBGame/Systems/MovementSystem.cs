using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Tecmo-style movement system.
/// Instant direction changes, speed ramps to max, instant stop.
/// </summary>
public class MovementSystem : EntityUpdateSystem
{
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<VelocityComponent> _velocityMapper;
    private ComponentMapper<BehaviorComponent> _behaviorMapper;

    public MovementSystem() : base(Aspect.All(typeof(PositionComponent), typeof(VelocityComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _velocityMapper = mapperService.GetMapper<VelocityComponent>();
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        foreach (var entityId in ActiveEntities)
        {
            var position = _positionMapper.Get(entityId);
            var velocity = _velocityMapper.Get(entityId);
            
            // Get target direction from behavior
            Vector2 targetDirection = GetTargetDirection(entityId);
            
            if (targetDirection == Vector2.Zero)
            {
                // INSTANT stop (no momentum)
                velocity.Velocity = Vector2.Zero;
            }
            else
            {
                // Snap to input direction, ramp speed
                var targetVelocity = targetDirection * velocity.MaxSpeed;
                velocity.Velocity = Vector2.Lerp(velocity.Velocity, targetVelocity, velocity.Acceleration);
            }
            
            // Apply velocity
            position.Position += velocity.Velocity;
        }
    }
    
    private Vector2 GetTargetDirection(int entityId)
    {
        if (!_behaviorMapper.Has(entityId))
            return Vector2.Zero;
            
        var behavior = _behaviorMapper.Get(entityId);
        
        switch (behavior.State)
        {
            case BehaviorState.Idle:
                return Vector2.Zero;
                
            case BehaviorState.MovingToPosition:
            case BehaviorState.RushingQB:
            case BehaviorState.RunningRoute:
                var direction = behavior.TargetPosition - _positionMapper.Get(entityId).Position;
                if (direction.LengthSquared() > 1f)
                {
                    direction.Normalize();
                    return direction;
                }
                return Vector2.Zero;
                
            default:
                return Vector2.Zero;
        }
    }
}
