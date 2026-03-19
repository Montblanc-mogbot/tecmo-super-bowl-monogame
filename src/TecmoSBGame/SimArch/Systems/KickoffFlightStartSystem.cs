using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/KickoffAfterScoreSystem.cs
// Ported from: src/TecmoSBGame/ArchiveMge/Systems/KickoffCoverageSystem.cs
// Ported from: src/TecmoSBGame/ArchiveMge/Systems/KickoffReturnSystem.cs

/// <summary>
/// SimArch kickoff flight start stub.
///
/// This provides a deterministic way to start a kickoff-style flight using the same
/// parametric flight fields on <see cref="Ball"/>.
///
/// Full kickoff setup/coverage/return systems are not yet ported to SimArch.
/// </summary>
public static class KickoffFlightStartSystem
{
    public static void StartKickoff(World world, int ballEntityId, Vector2 start, Vector2 end, float durationSeconds = 1.2f, float apexHeight = 18f)
    {
        var q = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in q, (Entity e, ref Ball b, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = TecmoSBGame.SimArch.Components.BallState.InAir;
            b.OwnerEntityId = -1;

            b.FlightKind = BallFlightKind.Kickoff;
            b.StartPos = start;
            b.EndPos = end;
            b.DurationSeconds = durationSeconds;
            b.ElapsedSeconds = 0f;
            b.ApexHeight = apexHeight;
            b.Height = 0f;
            b.IsComplete = false;

            pos.Value = start;
            vel.Value = Vector2.Zero;
        });
    }
}
