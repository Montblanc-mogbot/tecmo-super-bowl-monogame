using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Handles player input for human-controlled entities.
/// Supports keyboard (arrow keys + space) and gamepad.
/// </summary>
public class InputSystem : EntityUpdateSystem
{
    private readonly State.LoopState? _loop;

    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<BehaviorComponent> _behaviorMapper;
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    private ComponentMapper<PlayerControlComponent> _controlMapper;
    private ComponentMapper<MovementInputComponent> _moveInputMapper;
    private ComponentMapper<MovementActionComponent> _actionMapper;
    private ComponentMapper<PlayerAttributesComponent> _attrMapper;
    private ComponentMapper<PlayerActionStateComponent> _playerActionMapper;

    public InputSystem(State.LoopState? loop = null)
        : base(Aspect.All(typeof(TeamComponent), typeof(BehaviorComponent), typeof(PlayerControlComponent)))
    {
        _loop = loop;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
        _controlMapper = mapperService.GetMapper<PlayerControlComponent>();
        _moveInputMapper = mapperService.GetMapper<MovementInputComponent>();
        _actionMapper = mapperService.GetMapper<MovementActionComponent>();
        _attrMapper = mapperService.GetMapper<PlayerAttributesComponent>();
        _playerActionMapper = mapperService.GetMapper<PlayerActionStateComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Gating example: only accept input during pre-snap or live play.
        // (Rendering and other systems continue to run regardless.)
        if (_loop is not null && !_loop.IsOnField("pre_snap", "live_play"))
            return;

        var keyboard = Keyboard.GetState();
        var gamepad = GamePad.GetState(PlayerIndex.One);

        foreach (var entityId in ActiveEntities)
        {
            var team = _teamMapper.Get(entityId);
            
            // Only process input for the single currently controlled entity on the player-controlled team.
            if (!team.IsPlayerControlled)
                continue;

            if (!_controlMapper.Get(entityId).IsControlled)
                continue;

            var behavior = _behaviorMapper.Get(entityId);
            var hasBall = _ballMapper.Has(entityId) && _ballMapper.Get(entityId).HasBall;

            // Get input direction
            Vector2 inputDirection = GetInputDirection(keyboard, gamepad);

            // Store explicit movement input intent for the MovementSystem.
            if (_moveInputMapper.Has(entityId))
                _moveInputMapper.Get(entityId).Direction = inputDirection;

            // Keep behavior updated as well (useful for other systems / debugging),
            // but MovementSystem will prefer MovementInput for the controlled entity.
            if (inputDirection != Vector2.Zero)
            {
                behavior.State = BehaviorState.MovingToPosition;
                behavior.TargetPosition = _positionMapper.Get(entityId).Position + inputDirection * 100f;
            }
            else
            {
                behavior.State = BehaviorState.Idle;
            }

            // ---- Translate raw buttons into high-level action commands (contextual) ----
            // NOTE: We use edge detection so holding a button doesn't spam requests each tick.
            if (_playerActionMapper.Has(entityId))
            {
                var pa = _playerActionMapper.Get(entityId);

                var actionDown = keyboard.IsKeyDown(Keys.Space) || gamepad.Buttons.A == ButtonState.Pressed;
                var pitchDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl) || gamepad.Buttons.X == ButtonState.Pressed;
                var sprintDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) || gamepad.Buttons.B == ButtonState.Pressed;
                var jukeDown = keyboard.IsKeyDown(Keys.C) || gamepad.Buttons.Y == ButtonState.Pressed;

                var actionPressed = actionDown && !pa.PrevActionDown;
                var pitchPressed = pitchDown && !pa.PrevPitchDown;
                var sprintPressed = sprintDown && !pa.PrevSprintDown;
                var jukePressed = jukeDown && !pa.PrevJukeDown;

                pa.PrevActionDown = actionDown;
                pa.PrevPitchDown = pitchDown;
                pa.PrevSprintDown = sprintDown;
                pa.PrevJukeDown = jukeDown;

                if (actionPressed || pitchPressed || sprintPressed || jukePressed)
                {
                    var cmd = ResolveCommandForContext(entityId, team, hasBall, inputDirection, actionPressed, pitchPressed, sprintPressed, jukePressed);
                    if (cmd != PlayerActionCommand.None)
                        pa.PendingCommand = cmd;
                }
            }

            // Back-compat: if the entity does not have the action-command component,
            // fall back to the old direct MovementAction behavior.
            if (!_playerActionMapper.Has(entityId))
            {
                if (keyboard.IsKeyDown(Keys.Space) || gamepad.Buttons.A == ButtonState.Pressed)
                    OnActionPressed(entityId, hasBall);
            }
        }
    }

    private Vector2 GetInputDirection(KeyboardState keyboard, GamePadState gamepad)
    {
        Vector2 direction = Vector2.Zero;

        // Keyboard input
        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
            direction.Y -= 1;
        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
            direction.Y += 1;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            direction.X -= 1;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            direction.X += 1;

        // Gamepad left stick or D-pad
        if (direction == Vector2.Zero)
        {
            direction = gamepad.ThumbSticks.Left;
            direction.Y *= -1; // Invert Y for gamepad (up is negative in MonoGame)

            if (gamepad.DPad.Up == ButtonState.Pressed)
                direction.Y -= 1;
            if (gamepad.DPad.Down == ButtonState.Pressed)
                direction.Y += 1;
            if (gamepad.DPad.Left == ButtonState.Pressed)
                direction.X -= 1;
            if (gamepad.DPad.Right == ButtonState.Pressed)
                direction.X += 1;
        }

        // Normalize
        if (direction.LengthSquared() > 1f)
            direction.Normalize();

        return direction;
    }

    private PlayerActionCommand ResolveCommandForContext(
        int entityId,
        TeamComponent team,
        bool hasBall,
        Vector2 inputDirection,
        bool actionPressed,
        bool pitchPressed,
        bool sprintPressed,
        bool jukePressed)
    {
        // Pitch has highest priority when pressed.
        if (pitchPressed && hasBall && team.IsOffense)
            return PlayerActionCommand.Pitch;

        if (sprintPressed)
            return PlayerActionCommand.SprintBurst;

        if (jukePressed)
            return PlayerActionCommand.JukeCut;

        if (!actionPressed)
            return PlayerActionCommand.None;

        // Action button is context sensitive.
        if (!team.IsOffense)
        {
            // Defense: tackle attempt.
            return PlayerActionCommand.Tackle;
        }

        // Offense.
        if (hasBall)
        {
            var isQb = false;
            if (_attrMapper.Has(entityId))
            {
                var pos = (_attrMapper.Get(entityId).Position ?? string.Empty).Trim();
                isQb = string.Equals(pos, "QB", System.StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(pos, "Quarterback", System.StringComparison.OrdinalIgnoreCase);
            }

            if (isQb)
            {
                // Minimal: if the QB is actively steering when Action is pressed, treat as a scramble.
                // Otherwise treat as a pass request.
                return inputDirection != Vector2.Zero ? PlayerActionCommand.Scramble : PlayerActionCommand.Pass;
            }

            // Ball carrier (RB/WR/etc): dive.
            return PlayerActionCommand.Dive;
        }

        // Offense but not holding ball: no-op for now (catch attempts later).
        return PlayerActionCommand.None;
    }

    private void OnActionPressed(int entityId, bool hasBall)
    {
        // Hook point only: set an action state with timers/cooldowns.
        if (!_actionMapper.Has(entityId))
            return;

        var a = _actionMapper.Get(entityId);
        if (a.CooldownTimer > 0f)
            return;

        if (hasBall)
        {
            a.State = MovementActionState.Dive;
            a.StateTimer = a.DiveDurationSeconds;
            a.CooldownTimer = a.DiveCooldownSeconds;
        }
        else
        {
            a.State = MovementActionState.Burst;
            a.StateTimer = a.BurstDurationSeconds;
            a.CooldownTimer = a.BurstCooldownSeconds;
        }
    }
}
