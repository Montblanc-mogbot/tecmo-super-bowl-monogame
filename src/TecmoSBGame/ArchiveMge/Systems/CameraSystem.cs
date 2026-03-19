using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Rendering;

namespace TecmoSBGame.Systems;

/// <summary>
/// Maintains a shared Camera2D instance based on gameplay state.
///
/// Tecmo-style scrolling (first pass):
/// - Choose a follow target by CameraTargetComponent.Priority
/// - Apply deadzone scrolling in view-space
/// - Clamp to a world bounds rectangle
///
/// Rendering composes Camera2D view matrix with RenderViewport.ScaleMatrix.
/// </summary>
public sealed class CameraSystem : EntityUpdateSystem
{
    private readonly Camera2D _camera;
    private readonly Rectangle _worldBounds;

    private readonly Rectangle _deadzone;
    private readonly float _followGainPerTick;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<CameraTargetComponent> _target = null!;

    public CameraSystem(
        Camera2D camera,
        Rectangle? worldBounds = null,
        Rectangle? deadzone = null,
        float followGainPerTick = 0.20f)
        : base(Aspect.All(typeof(CameraTargetComponent), typeof(PositionComponent)))
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _worldBounds = worldBounds ?? new Rectangle(0, 0, 256, 224);

        _deadzone = deadzone ?? new Rectangle(x: 96, y: 84, width: 64, height: 56);
        _followGainPerTick = MathHelper.Clamp(followGainPerTick, 0f, 1f);
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _target = mapperService.GetMapper<CameraTargetComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (ActiveEntities.Count == 0)
            return;

        var bestId = -1;
        var bestPriority = int.MinValue;

        foreach (var entityId in ActiveEntities)
        {
            var p = _target.Get(entityId).Priority;
            if (p > bestPriority)
            {
                bestPriority = p;
                bestId = entityId;
            }
        }

        if (bestId < 0)
            return;

        var targetPos = _pos.Get(bestId).Position;

        ApplyDeadzoneFollow(targetPos);
        _camera.ClampToBounds(_worldBounds);
    }

    private void ApplyDeadzoneFollow(Vector2 targetWorld)
    {
        var targetView = targetWorld - _camera.Position;

        var dz = _deadzone;
        var newCamPos = _camera.Position;

        if (targetView.X < dz.Left)
            newCamPos.X -= (dz.Left - targetView.X);
        else if (targetView.X > dz.Right)
            newCamPos.X += (targetView.X - dz.Right);

        if (targetView.Y < dz.Top)
            newCamPos.Y -= (dz.Top - targetView.Y);
        else if (targetView.Y > dz.Bottom)
            newCamPos.Y += (targetView.Y - dz.Bottom);

        var smoothed = Vector2.Lerp(_camera.Position, newCamPos, _followGainPerTick);
        _camera.Position = new Vector2(MathF.Round(smoothed.X), MathF.Round(smoothed.Y));
    }
}
