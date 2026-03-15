using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Field goal block rush timing — scaffold.
///
/// Behavior:
/// - Active only during special-teams slice (no-pass) while play is live.
/// - Entities with FieldGoalBlockRushComponent wait DelayFrames, then drive Behavior
///   to rush forward along RushDirection.
///
/// This is intentionally generic until a proper FG play phase/type is introduced.
/// </summary>
public sealed class FieldGoalBlockRushSystem : EntityUpdateSystem
{
    private readonly PlayState _play;

    private ComponentMapper<FieldGoalBlockRushComponent> _fg = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PositionComponent> _pos = null!;

    public FieldGoalBlockRushSystem(PlayState play)
        : base(Aspect.All(typeof(FieldGoalBlockRushComponent), typeof(BehaviorComponent), typeof(PositionComponent)))
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _fg = mapperService.GetMapper<FieldGoalBlockRushComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.Phase != PlayPhase.InPlay || _play.IsOver)
            return;

        // Special-teams slice only (no passing).
        if (_play.AllowPass)
            return;

        foreach (var id in ActiveEntities)
        {
            var fg = _fg.Get(id);
            fg.ElapsedFrames++;

            if (fg.ElapsedFrames < Math.Max(0, fg.DelayFrames))
                continue;

            var p = _pos.Get(id).Position;
            var dir = fg.RushDirection;
            if (dir.LengthSquared() < 0.001f)
                dir = new Vector2(1, 0);
            else
                dir.Normalize();

            // Move forward ~2px per tick.
            var target = p + dir * 16f;

            var b = _behavior.Get(id);
            b.State = BehaviorState.MovingToPosition;
            b.TargetPosition = target;
        }
    }
}
