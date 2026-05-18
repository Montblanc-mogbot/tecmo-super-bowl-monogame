using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Resolves fumble events into loose ball state.
///
/// SimArch tackle resolution already drops the ball and gives it loose-ball velocity;
/// this system is the authoritative cleanup point that keeps the loose-ball state sane.
/// </summary>
public sealed class FumbleResolutionSystem
{
    public void Update(World world, int ballEntityId)
    {
        var qBall = new QueryDescription().WithAll<Ball, Velocity>();
        world.Query(in qBall, (Entity e, ref Ball ball, ref Velocity velocity) =>
        {
            if (e.Id != ballEntityId)
                return;

            if (ball.State != BallState.Loose)
                return;

            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.None;
            ball.Height = 0f;

            if (velocity.Value.LengthSquared() < 0.0001f)
                velocity.Value = new Vector2(0.9f, 0.2f);
        });
    }
}
