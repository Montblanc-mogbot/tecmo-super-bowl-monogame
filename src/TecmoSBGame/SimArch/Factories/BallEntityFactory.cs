using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Factories;

/// <summary>
/// Factory for creating the dedicated ball entity (SimArch).
///
/// Ported from: ArchiveMge/Factories/BallEntityFactory.cs
/// </summary>
public static class BallEntityFactory
{
    public static int CreateBall(World world, Vector2 position)
    {
        var e = world.Create();

        e.Add(new Ball
        {
            State = BallState.Dead,
            OwnerEntityId = 0,
            FlightKind = BallFlightKind.None,
            PasserEntityId = 0,
            TargetEntityId = 0,
            PassType = TecmoSBGame.SimArch.PassType.Bullet,
            StartPos = position,
            EndPos = position,
            ElapsedSeconds = 0f,
            DurationSeconds = 0f,
            ApexHeight = 0f,
            Height = 0f,
            IsComplete = false,
        });

        e.Add(new Position { Value = position });

        // Reuse velocity as an inert data carrier.
        e.Add(new Velocity { Value = Vector2.Zero });

        e.Add(Sprite.Create("ball"));
        e.Add(new CameraTarget { Priority = 50 });
        e.Add(new AiDebugDrawable());

        return e.Id;
    }
}
