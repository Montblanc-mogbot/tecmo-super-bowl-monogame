using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Headless;

/// <summary>
/// Headless-only driver that triggers a deterministic pass at a fixed tick.
/// This is used to verify coverage break behavior without input/UI.
/// </summary>
public sealed class CoverageScenarioDriverSystem : UpdateSystem
{
    private readonly GameEvents _events;
    private readonly PlayState _play;
    private readonly int _passerId;
    private readonly int _targetId;
    private readonly int _throwAtTick;

    private int _tick;
    private bool _thrown;

    public CoverageScenarioDriverSystem(GameEvents events, PlayState play, int passerId, int targetId, int throwAtTick = 90)
    {
        _events = events;
        _play = play;
        _passerId = passerId;
        _targetId = targetId;
        _throwAtTick = throwAtTick;
    }

    public override void Update(GameTime gameTime)
    {
        _tick++;

        // Mark play in progress for systems that gate on PlayState.
        if (_play.Phase == PlayPhase.PreSnap)
            _play.Phase = PlayPhase.InPlay;

        if (_thrown)
            return;

        if (_tick >= _throwAtTick)
        {
            _events.Publish(new PassRequestedEvent(PasserId: _passerId, TargetId: _targetId, PassType: PassType.Bullet));
            _thrown = true;
        }
    }
}
