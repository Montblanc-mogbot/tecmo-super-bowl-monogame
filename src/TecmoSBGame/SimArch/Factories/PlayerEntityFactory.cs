using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Factories;

/// <summary>
/// Factory for creating player entities with consistent configuration.
///
/// Ported from: ArchiveMge/Factories/PlayerEntityFactory.cs
/// </summary>
public static class PlayerEntityFactory
{
    public static int CreatePlayer(
        World world,
        Vector2 position,
        int teamIndex,
        bool isPlayerControlled,
        bool isOffense,
        string spriteId = "player_placeholder",
        float maxSpeed = 2.5f,
        float acceleration = 0.3f)
    {
        var e = world.Create();

        e.Add(new Position { Value = position });
        e.Add(new Velocity { Value = Vector2.Zero });

        e.Add(MovementTuning.Create(
            maxSpeedPerTick: maxSpeed,
            accelPerTick: MathHelper.Clamp(acceleration, 0.05f, 1f) * maxSpeed,
            decelPerTick: maxSpeed * 4.0f,
            cutPenalty: 0.25f,
            burstMultiplier: 1.20f));

        e.Add(new MovementInput { Direction = Vector2.Zero });
        e.Add(MovementAction.Default);
        e.Add(SpeedModifier.Default);

        e.Add(new PlayerActionState
        {
            PendingCommand = PlayerActionCommand.None,
            PendingTargetEntityId = -1,
            LastAppliedCommand = PlayerActionCommand.None,
            LastAppliedTargetEntityId = -1,
        });

        e.Add(new Team { TeamIndex = teamIndex, IsPlayerControlled = isPlayerControlled, IsOffense = isOffense });

        e.Add(new Behavior
        {
            State = BehaviorState.Idle,
            TargetEntityId = -1,
            TargetPosition = position,
            StateTimer = 0f,
        });

        e.Add(new BehaviorStack());
        e.Add(new Engagement { PartnerEntityId = -1, CooldownSeconds = 0f });

        e.Add(Sprite.Create(spriteId));
        e.Add(AnimationState.CreateWithDefaultPlayerClips(spriteId));

        e.Add(new BallCarrier { HasBall = false, YardsAfterCatch = 0f });
        e.Add(new PlayerControl { IsControlled = false });
        e.Add(new AiDebugDrawable());

        e.Add(new CameraTarget { Priority = isPlayerControlled ? 100 : 10 });

        return e.Id;
    }

    // NOTE: The legacy factory includes a large amount of formation/kickoff roster creation.
    // That responsibility is expected to live in SimArch spawners (FormationSpawner / PlaySpawner).
}
