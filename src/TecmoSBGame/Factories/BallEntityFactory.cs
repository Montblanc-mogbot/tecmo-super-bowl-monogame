using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Factories;

/// <summary>
/// Factory for creating the dedicated ball entity.
/// </summary>
public static class BallEntityFactory
{
    public static int CreateBall(World world, Vector2 position)
    {
        var entity = world.CreateEntity();

        entity.Attach(new BallComponent());
        entity.Attach(new BallStateComponent(BallState.Dead));
        entity.Attach(new BallOwnerComponent(ownerEntityId: null));

        entity.Attach(new PositionComponent(position));

        // Always attach a flight component so systems can overwrite it deterministically
        // without requiring runtime attach support.
        entity.Attach(new BallFlightComponent(BallFlightKind.None, position, position, durationSeconds: 0f, apexHeight: 0f));

        // Reuse VelocityComponent but do not drive it through MovementSystem.
        // (Ball motion is currently handled by the kickoff slice / ball sync logic.)
        entity.Attach(new VelocityComponent(maxSpeed: 10f, acceleration: 0f));

        // Keep SpriteComponent optional; headless uses only data.
        // entity.Attach(new SpriteComponent("ball"));

        return entity.Id;
    }
}
