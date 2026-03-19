using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Collects AI decision data for debug rendering.
///
/// This system does not draw; it writes data into AIDebugDrawableComponent so MainGame (or a renderer)
/// can visualize routes, coverage targets, and behavior target positions.
/// </summary>
public sealed class AIDebugSystem : EntityUpdateSystem
{
    private readonly AIDebugConfigComponent _config;

    private ComponentMapper<AIDebugDrawableComponent> _dbg = null!;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<RouteComponent> _route = null!;
    private ComponentMapper<CoverageComponent> _coverage = null!;

    public AIDebugSystem(AIDebugConfigComponent config)
        : base(Aspect.All(typeof(AIDebugDrawableComponent), typeof(PositionComponent)))
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _dbg = mapperService.GetMapper<AIDebugDrawableComponent>();

        _pos = mapperService.GetMapper<PositionComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _route = mapperService.GetMapper<RouteComponent>();
        _coverage = mapperService.GetMapper<CoverageComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var enabled = true;
        var showRoutes = true;
        var showTargets = true;
        var showCoverage = true;
        var focus = -1;

        enabled = _config.Enabled;
        showRoutes = _config.ShowRoutes;
        showTargets = _config.ShowBehaviorTargets;
        showCoverage = _config.ShowCoverage;
        focus = _config.FocusEntityId;

        foreach (var id in ActiveEntities)
        {
            var d = _dbg.Get(id);
            if (!enabled || (focus != -1 && id != focus))
            {
                d.Visible = false;
                continue;
            }

            d.Visible = true;
            d.TargetPosition = null;
            d.Label = null;
            d.RouteOrigin = null;
            d.RouteNextTarget = null;
            d.ManTargetEntityId = -1;
            d.ZoneLandmark = null;

            if (showTargets && _behavior.Has(id))
            {
                var b = _behavior.Get(id);
                d.TargetPosition = b.TargetPosition;
            }

            if (showRoutes && _route.Has(id))
            {
                var r = _route.Get(id);
                d.RouteOrigin = r.Origin;

                if (!r.RouteComplete && r.Nodes.Count > 0)
                {
                    var idx = Math.Clamp(r.CurrentNodeIndex, 0, r.Nodes.Count - 1);
                    var node = r.Nodes[idx];
                    d.RouteNextTarget = r.Origin + node.Offset;
                }
            }

            if (showCoverage && _coverage.Has(id))
            {
                var c = _coverage.Get(id);
                if (c.Type == CoverageType.ManToMan)
                    d.ManTargetEntityId = c.AssignmentTargetId;
                else
                    d.ZoneLandmark = c.LandmarkPosition;
            }

            // Minimal label.
            var p = _pos.Get(id).Position;
            d.Label = $"{id} ({(int)p.X},{(int)p.Y})";
        }
    }
}
