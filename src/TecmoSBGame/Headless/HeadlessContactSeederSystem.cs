using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Systems;

namespace TecmoSBGame.Headless;

/// <summary>
/// Headless-only helper to ensure we observe at least one contact event during a smoke run.
///
/// The main game depends on player input for the returner, so in a windowless run
/// the return scenario can stall without any interactions.
///
/// This system deterministically places one defender into tackle-range of the current
/// ball carrier once, after possession is established.
/// </summary>
public sealed class HeadlessContactSeederSystem : EntityUpdateSystem
{
    private ComponentMapper<PositionComponent> _pos;
    private ComponentMapper<TeamComponent> _team;
    private ComponentMapper<BallCarrierComponent> _ball;

    private bool _seeded;

    public HeadlessContactSeederSystem()
        : base(Aspect.All(typeof(PositionComponent), typeof(TeamComponent), typeof(BallCarrierComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _ball = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_seeded)
            return;

        // Find ball carrier.
        var carrierId = -1;
        foreach (var id in ActiveEntities)
        {
            if (_ball.Get(id).HasBall)
            {
                carrierId = id;
                break;
            }
        }

        if (carrierId == -1)
            return;

        var carrierTeam = _team.Get(carrierId);
        var carrierPos = _pos.Get(carrierId).Position;

        // Find any defender (opposing team and not offense).
        var defenderId = -1;
        foreach (var id in ActiveEntities)
        {
            if (id == carrierId)
                continue;

            var t = _team.Get(id);
            if (t.TeamIndex == carrierTeam.TeamIndex)
                continue;

            if (t.IsOffense)
                continue;

            defenderId = id;
            break;
        }

        if (defenderId == -1)
            return;

        // Place defender just inside base tackle radius.
        _pos.Get(defenderId).Position = carrierPos + new Vector2(CollisionContactSystem.TACKLE_CONTACT_RADIUS_BASE - 0.5f, 0f);
        _seeded = true;
    }
}
