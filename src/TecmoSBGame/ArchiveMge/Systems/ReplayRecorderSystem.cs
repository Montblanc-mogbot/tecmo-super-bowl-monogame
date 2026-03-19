using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Replay;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Captures per-tick snapshots into <see cref="ReplayRecorder"/>.
///
/// This is a non-deterministic side-effect (file output) but the captured frames should be
/// deterministic given the same sim.
/// </summary>
public sealed class ReplayRecorderSystem : EntityUpdateSystem
{
    private readonly PlayState _play;
    private readonly ReplayRecorder _rec;

    private ComponentMapper<PositionComponent> _pos = null!;

    private int _lastPlayId;

    public ReplayRecorderSystem(PlayState play, ReplayRecorder recorder)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _rec = recorder ?? throw new ArgumentNullException(nameof(recorder));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!_rec.Enabled)
            return;

        // Start-of-play reset.
        if (_play.PlayId != _lastPlayId)
        {
            _lastPlayId = _play.PlayId;
            _rec.ResetForPlay(_play);
        }

        if (_play.Phase != PlayPhase.InPlay)
            return;

        var tick = (int)MathF.Floor(_play.PlayElapsedSeconds * 60f);

        // Avoid duplicate frames if dt hiccups.
        if (_rec.Capture.Frames.Count > 0 && _rec.Capture.Frames[^1].Tick == tick)
            return;

        var frame = new ReplayFrame { Tick = tick };
        frame.Ball.State = _play.BallState.ToString();
        frame.Ball.OwnerEntityId = _play.BallOwnerEntityId;

        // Capture positions for all entities with PositionComponent.
        foreach (var id in ActiveEntities)
        {
            var p = _pos.Get(id).Position;
            frame.Positions[id.ToString()] = new ReplayPos(p.X, p.Y);
        }

        _rec.Capture.Frames.Add(frame);

        // Flush once per play end (when whistle occurs).
        if (_play.IsOver)
        {
            try
            {
                var path = _rec.SaveJson();
                Console.WriteLine($"[replay] wrote {frame.Tick + 1} ticks -> {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[replay] save failed: {ex.Message}");
            }

            _rec.Enabled = false; // one-shot by default
        }
    }
}
