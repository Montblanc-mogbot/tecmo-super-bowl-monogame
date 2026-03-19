using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// SimArch kickoff flight completion stub.
///
/// For now: when a kickoff flight completes, the ball becomes loose.
/// Return logic / touchbacks / out-of-bounds are future work.
/// </summary>
public sealed class KickoffFlightCompleteSystem
{
    public void Update(World world)
    {
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity _, ref Ball b) =>
        {
            if (b.FlightKind != BallFlightKind.Kickoff || !b.IsComplete)
                return;

            b.FlightKind = BallFlightKind.None;
            b.State = TecmoSBGame.SimArch.Components.BallState.Loose;
            b.OwnerEntityId = -1;
            b.Height = 0f;
        });
    }
}
