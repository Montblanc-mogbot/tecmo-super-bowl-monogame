using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Headless-friendly AI decision logging.
///
/// Intended for deterministic verification runs where we want to see:
/// - QB read index / timers
/// - receiver route node targets
/// - defender coverage targets
///
/// This system is read-only and safe to add in non-headless runs, but is mainly wired into HeadlessRunner.
/// </summary>
public sealed class AIDecisionLogSystem : EntityUpdateSystem
{
    private readonly PlayState _play;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<RouteComponent> _route = null!;
    private ComponentMapper<CoverageComponent> _cov = null!;
    private ComponentMapper<QbBrainComponent> _qb = null!;

    private int _nextLogFrame;

    public AIDecisionLogSystem(PlayState play)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _route = mapperService.GetMapper<RouteComponent>();
        _cov = mapperService.GetMapper<CoverageComponent>();
        _qb = mapperService.GetMapper<QbBrainComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.Phase != PlayPhase.InPlay)
            return;

        var frame = (int)MathF.Floor(_play.PlayElapsedSeconds * 60f);
        if (frame < _nextLogFrame)
            return;

        _nextLogFrame = frame + 15; // ~4 logs/sec

        // Log QB state (first qb we find).
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var id = ActiveEntities[i];
            if (!_qb.Has(id) || !_pos.Has(id))
                continue;

            var qb = _qb.Get(id);
            var p = _pos.Get(id).Position;
            Console.WriteLine($"[ai] t={frame,3} qb={id} pos=({p.X:0.0},{p.Y:0.0}) drop={qb.DropbackStep}/{qb.GetTotalStepCount()} read={qb.CurrentReadIndex} scramble={qb.ScrambleMode} pressure={qb.PressureFrameCount}");
            break;
        }

        // Log a few receivers: routes + current next target.
        var logged = 0;
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            if (logged >= 4)
                break;

            var id = ActiveEntities[i];
            if (!_route.Has(id) || !_pos.Has(id))
                continue;

            var r = _route.Get(id);
            var p = _pos.Get(id).Position;
            var idx = Math.Clamp(r.CurrentNodeIndex, 0, Math.Max(0, r.Nodes.Count - 1));

            var next = r.Nodes.Count > 0 ? r.Origin + r.Nodes[idx].Offset : r.Origin;
            Console.WriteLine($"[ai] t={frame,3} wr={id} pos=({p.X:0.0},{p.Y:0.0}) node={r.CurrentNodeIndex}/{r.Nodes.Count} next=({next.X:0.0},{next.Y:0.0}) sitting={r.IsSitting} done={r.RouteComplete}");
            logged++;
        }

        // Log one defender coverage target (first we find).
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var id = ActiveEntities[i];
            if (!_cov.Has(id) || !_pos.Has(id))
                continue;

            var c = _cov.Get(id);
            var p = _pos.Get(id).Position;

            if (c.Type == CoverageType.ManToMan)
                Console.WriteLine($"[ai] t={frame,3} def={id} pos=({p.X:0.0},{p.Y:0.0}) cov=man tgt={c.AssignmentTargetId} react={c.ReactionTimer}/{c.ReactionDelay}");
            else
                Console.WriteLine($"[ai] t={frame,3} def={id} pos=({p.X:0.0},{p.Y:0.0}) cov=zone lm=({c.LandmarkPosition.X:0.0},{c.LandmarkPosition.Y:0.0}) react={c.ReactionTimer}/{c.ReactionDelay}");

            break;
        }
    }
}
