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

        entity.Attach(new BallComponent(BallState.Dead));

        entity.Attach(new PositionComponent(position));


        // Reuse VelocityComponent but do not drive it through MovementSystem.
        // (Ball motion is currently handled by the kickoff slice / ball sync logic.)
        entity.Attach(new VelocityComponent(maxSpeed: 10f, acceleration: 0f));

        // Attach a sprite marker so debug rendering can show the ball.
        // (Still fine for headless: the component is inert and only read by rendering.)
        entity.Attach(new SpriteComponent("ball"));
        entity.Attach(new CameraTargetComponent(priority: 50));
        entity.Attach(new AIDebugDrawableComponent());

        return entity.Id;
    }
}
