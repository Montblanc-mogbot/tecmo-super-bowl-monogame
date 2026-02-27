using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Decrements timers for <see cref="SpeedModifierComponent"/>.
/// (We intentionally do not detach components to keep ECS usage simple; inactive modifiers are ignored.)
/// </summary>
public sealed class SpeedModifierSystem : EntityUpdateSystem
{
    private ComponentMapper<SpeedModifierComponent> _mod = null!;

    public SpeedModifierSystem()
        : base(Aspect.All(typeof(SpeedModifierComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _mod = mapperService.GetMapper<SpeedModifierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt <= 0f)
            return;

        foreach (var id in ActiveEntities)
        {
            var m = _mod.Get(id);
            if (m.TimerSeconds > 0f)
                m.TimerSeconds -= dt;
        }
    }
}
