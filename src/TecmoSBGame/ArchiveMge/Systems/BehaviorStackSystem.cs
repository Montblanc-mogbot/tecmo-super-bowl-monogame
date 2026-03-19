using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Decrements the active interrupt timer (top of stack) and restores the prior behavior when it expires.
/// </summary>
public sealed class BehaviorStackSystem : EntityUpdateSystem
{
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;

    public BehaviorStackSystem() : base(Aspect.All(typeof(BehaviorComponent), typeof(BehaviorStackComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt <= 0f)
            return;

        foreach (var entityId in ActiveEntities)
        {
            var stack = _stack.Get(entityId);
            if (!stack.TryPeek(out var top))
                continue;

            var remaining = top.RemainingSeconds - dt;
            if (remaining > 0f)
            {
                // Update remaining time (replace top entry).
                stack.Stack[^1] = top with { RemainingSeconds = remaining };
                continue;
            }

            // Expired: pop and restore.
            if (!stack.TryPop(out var popped))
                continue;

            var behavior = _behavior.Get(entityId);
            BehaviorInterrupt.Restore(behavior, popped.Saved);

            Console.WriteLine($"[interrupt] end kind={popped.Kind} entity={entityId}");
        }
    }
}
