using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Drives <see cref="BehaviorComponent"/> targets for entities that have a <see cref="RouteComponent"/>.
///
/// Design goals:
/// - Deterministic: no randomness.
/// - Frame-based timing: node transitions depend on frame counters only.
/// - Tecmo-style cuts: snap to the break point on the exact frame the node completes.
/// - Integrates with the existing <see cref="MovementSystem"/> (this system sets Behavior.TargetPosition;
///   MovementSystem computes desired direction and applies speed/accel).
///
/// NOTE: This is a route-follow scaffold until the ROM route byte tables are imported.
/// Current play YAML does not encode per-segment frame counts, so defaults are used.
/// </summary>
public sealed class RouteFollowSystem : EntityUpdateSystem
{
    private const int TicksPerSecond = 60;
    private const float MaxMsInTsb = 69f;

    private ComponentMapper<RouteComponent> _routeMapper = null!;
    private ComponentMapper<PositionComponent> _posMapper = null!;
    private ComponentMapper<BehaviorComponent> _behaviorMapper = null!;
    private ComponentMapper<PlayerAttributesComponent> _attrMapper = null!;
    private ComponentMapper<MovementTuningComponent> _tuningMapper = null!;
    private ComponentMapper<DefensiveLookComponent> _lookMapper = null!;

    public RouteFollowSystem() : base(Aspect.All(typeof(RouteComponent), typeof(PositionComponent), typeof(BehaviorComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _routeMapper = mapperService.GetMapper<RouteComponent>();
        _posMapper = mapperService.GetMapper<PositionComponent>();
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
        _attrMapper = mapperService.GetMapper<PlayerAttributesComponent>();
        _tuningMapper = mapperService.GetMapper<MovementTuningComponent>();
        _lookMapper = mapperService.GetMapper<DefensiveLookComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Route timing is intended to be 60Hz; if dt drifts, we still advance exactly 1 "frame" per Update.
        // The surrounding app uses FixedTimestepRunner for headless and MonoGame fixed-step for main.
        _ = gameTime;

        foreach (var entityId in ActiveEntities)
        {
            var route = _routeMapper.Get(entityId);
            var pos = _posMapper.Get(entityId);
            var behavior = _behaviorMapper.Get(entityId);

            if (!route.Initialized)
            {
                route.Initialized = true;
                route.Origin = pos.Position;
                route.CurrentNodeIndex = Math.Clamp(route.CurrentNodeIndex, 0, Math.Max(0, route.Nodes.Count - 1));
                route.FrameCounter = 0;
                route.RouteComplete = false;
                route.IsSitting = false;

                ApplyRouteSpeed(entityId, route);
            }

            if (route.RouteComplete)
            {
                // Route ended; restore tuning if we modified it.
                RestoreRouteSpeed(entityId, route);
                if (behavior.State == BehaviorState.RunningRoute)
                    behavior.State = BehaviorState.Idle;
                continue;
            }

            if (route.Nodes.Count == 0)
            {
                // Nothing to do.
                behavior.State = BehaviorState.Idle;
                continue;
            }

            // If sitting, stop driving movement.
            if (route.IsSitting)
            {
                behavior.State = BehaviorState.Idle;
                continue;
            }

            var idx = Math.Clamp(route.CurrentNodeIndex, 0, route.Nodes.Count - 1);
            var node = route.Nodes[idx];

            // Node absolute target.
            var targetAbs = route.Origin + node.Offset;

            // ROUTE-5: crude deterministic route adjustments vs man/zone coverage.
            // We rely on DefensiveLookComponent attached by PlaySpawner.
            if (_lookMapper.Has(entityId))
            {
                var look = _lookMapper.Get(entityId);
                if (look.IsMan)
                    targetAbs += route.ManAdjustOffset;
                else if (look.IsZone)
                    targetAbs += route.ZoneAdjustOffset;
            }

            // Update behavior so MovementSystem moves toward our target.
            behavior.State = BehaviorState.RunningRoute;
            behavior.TargetPosition = targetAbs;

            // Advance route timing by exactly one frame per tick.
            route.FrameCounter++;

            // Handle depth/break timing.
            // Tecmo routes are timing-based; for now we treat the first node as the "stem" and allow
            // RouteComponent.StemFrames to override the first node's duration.
            var minFrames = Math.Max(0, node.MinFrames);
            if (idx == 0 && route.StemFrames > 0)
                minFrames = route.StemFrames;

            if (route.FrameCounter < minFrames)
                continue;

            var actionKind = node.ActionKind != RouteNodeAction.Run
                ? node.ActionKind
                : ParseAction(node.Action);

            if (actionKind == RouteNodeAction.Sit)
            {
                // Snap to sit point on the exact transition frame.
                pos.Position = targetAbs;
                route.IsSitting = true;
                behavior.State = BehaviorState.Idle;
                continue;
            }

            if (actionKind == RouteNodeAction.Return)
            {
                // Treat as route complete (ballcarrier-like behavior handled elsewhere).
                pos.Position = targetAbs;
                route.RouteComplete = true;
                continue;
            }

            // Tecmo-style cut: when the timing threshold is reached, snap to the break point and immediately
            // advance to the next node (direction changes next tick because MovementSystem reads new TargetPosition).
            // This is intentionally not distance-gated.
            pos.Position = targetAbs;

            route.FrameCounter = 0;
            if (route.CurrentNodeIndex < route.Nodes.Count - 1)
            {
                route.CurrentNodeIndex++;
            }
            else
            {
                // End-of-route behavior in Tecmo is usually "keep running"; but with finite nodes we mark complete.
                route.RouteComplete = true;
            }
        }
    }

    private static RouteNodeAction ParseAction(string? action)
    {
        var a = (action ?? string.Empty).Trim().ToUpperInvariant();
        return a switch
        {
            "CUT" => RouteNodeAction.Cut,
            "SIT" => RouteNodeAction.Sit,
            "RETURN" => RouteNodeAction.Return,
            _ => RouteNodeAction.Run,
        };
    }

    private void ApplyRouteSpeed(int entityId, RouteComponent route)
    {
        // RouteComponent.BaseSpeed is interpreted as "units/tick" at MS=69.
        // We translate to a MovementTuningComponent.MaxSpeedPerTick override.
        if (!_tuningMapper.Has(entityId) || route.BaseSpeed <= 0f)
            return;

        var tuning = _tuningMapper.Get(entityId);

        if (!route.SpeedApplied)
        {
            route.OriginalMaxSpeedPerTick = tuning.MaxSpeedPerTick;
            route.SpeedApplied = true;
        }

        // PlayerAttributesComponent.Ms in this project is currently a 0..100-style rating.
        // Convert to Tecmo's 0..69 scale for the requested formula.
        var ms = MaxMsInTsb;
        if (_attrMapper.Has(entityId))
        {
            var a = _attrMapper.Get(entityId);
            if (a.Ms > 0)
            {
                var rating = Math.Clamp(a.Ms, 0, 100);
                ms = (rating / 100f) * MaxMsInTsb;
            }
        }

        // ROUTE-6 formula request:
        // speed = (playerMS / 69f) * baseRouteSpeed
        var desired = (ms / MaxMsInTsb) * route.BaseSpeed;

        // Keep tuning coherent: accel/decel should scale proportionally when we override max speed.
        if (route.OriginalMaxSpeedPerTick > 0.0001f)
        {
            var ratio = desired / route.OriginalMaxSpeedPerTick;
            tuning.MaxSpeedPerTick = desired;
            tuning.AccelPerTick *= ratio;
            tuning.DecelPerTick *= ratio;
        }
        else
        {
            tuning.MaxSpeedPerTick = desired;
        }
    }

    private void RestoreRouteSpeed(int entityId, RouteComponent route)
    {
        if (!route.SpeedApplied)
            return;

        if (!_tuningMapper.Has(entityId))
            return;

        var tuning = _tuningMapper.Get(entityId);
        if (route.OriginalMaxSpeedPerTick > 0.0001f)
        {
            // Restore max speed, but don't attempt to perfectly restore accel/decel since we scaled them.
            // In practice, the route runner should persist for the play duration.
            tuning.MaxSpeedPerTick = route.OriginalMaxSpeedPerTick;
        }

        route.SpeedApplied = false;
    }
}
