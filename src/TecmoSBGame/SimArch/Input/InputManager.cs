using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TecmoSBGame.SimArch.Input;

/// <summary>
/// Centralized input manager that handles different game contexts.
/// Routes input based on current game state.
///
/// Ported from: ArchiveMge/Input/InputManager.cs
/// </summary>
public class InputManager
{
    public InputContext CurrentContext { get; private set; } = InputContext.Menu;

    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private GamePadState _currentGamePad;
    private GamePadState _previousGamePad;

    private float _repeatTimer = 0f;
    private const float REPEAT_DELAY = 0.3f;
    private const float REPEAT_INTERVAL = 0.1f;

    public event System.Action? OnMenuUp;
    public event System.Action? OnMenuDown;
    public event System.Action? OnMenuSelect;
    public event System.Action? OnMenuBack;

    public event System.Action? OnPlayCallUp;
    public event System.Action? OnPlayCallDown;
    public event System.Action? OnPlayCallLeft;
    public event System.Action? OnPlayCallRight;
    public event System.Action? OnPlayCallSelect;
    public event System.Action? OnPlayCallAudible;

    public event System.Action? OnPreSnapMotionLeft;
    public event System.Action? OnPreSnapMotionRight;
    public event System.Action? OnPreSnapHotRoute;
    public event System.Action? OnPreSnapSnap;

    public event System.Action<Vector2>? OnPlayerMove;
    public event System.Action? OnPlayerAction;
    public event System.Action? OnPlayerSpeedBurst;

    public event System.Action? OnPause;

    public void Update(GameTime gameTime)
    {
        _previousKeyboard = _currentKeyboard;
        _previousGamePad = _currentGamePad;
        _currentKeyboard = Keyboard.GetState();
        _currentGamePad = GamePad.GetState(PlayerIndex.One);

        HandleRepeats(gameTime);

        switch (CurrentContext)
        {
            case InputContext.Menu:
                HandleMenuInput();
                break;
            case InputContext.PlayCall:
                HandlePlayCallInput();
                break;
            case InputContext.PreSnap:
                HandlePreSnapInput();
                break;
            case InputContext.InPlay:
                HandleInPlayInput();
                break;
            case InputContext.PostPlay:
                HandlePostPlayInput();
                break;
        }

        if (IsPressed(Keys.Escape, Buttons.Start))
            OnPause?.Invoke();
    }

    public void SetContext(InputContext context)
    {
        CurrentContext = context;
        _repeatTimer = 0f;
    }

    private void HandleMenuInput()
    {
        if (IsPressed(Keys.Up, Buttons.DPadUp) || IsPressed(Keys.W, Buttons.LeftThumbstickUp))
            OnMenuUp?.Invoke();

        if (IsPressed(Keys.Down, Buttons.DPadDown) || IsPressed(Keys.S, Buttons.LeftThumbstickDown))
            OnMenuDown?.Invoke();

        if (IsPressed(Keys.Enter, Buttons.A))
            OnMenuSelect?.Invoke();

        if (IsPressed(Keys.Back, Buttons.B))
            OnMenuBack?.Invoke();
    }

    private void HandlePlayCallInput()
    {
        if (IsPressed(Keys.Up, Buttons.DPadUp) || IsPressed(Keys.W, Buttons.LeftThumbstickUp))
            OnPlayCallUp?.Invoke();

        if (IsPressed(Keys.Down, Buttons.DPadDown) || IsPressed(Keys.S, Buttons.LeftThumbstickDown))
            OnPlayCallDown?.Invoke();

        if (IsPressed(Keys.Left, Buttons.DPadLeft) || IsPressed(Keys.A, Buttons.LeftThumbstickLeft))
            OnPlayCallLeft?.Invoke();

        if (IsPressed(Keys.Right, Buttons.DPadRight) || IsPressed(Keys.D, Buttons.LeftThumbstickRight))
            OnPlayCallRight?.Invoke();

        if (IsPressed(Keys.Enter, Buttons.A))
            OnPlayCallSelect?.Invoke();

        if (IsPressed(Keys.LeftShift, Buttons.X))
            OnPlayCallAudible?.Invoke();
    }

    private void HandlePreSnapInput()
    {
        if (IsPressed(Keys.Left, Buttons.DPadLeft))
            OnPreSnapMotionLeft?.Invoke();

        if (IsPressed(Keys.Right, Buttons.DPadRight))
            OnPreSnapMotionRight?.Invoke();

        if (IsPressed(Keys.R, Buttons.Y))
            OnPreSnapHotRoute?.Invoke();

        if (IsPressed(Keys.Space, Buttons.A))
            OnPreSnapSnap?.Invoke();
    }

    private void HandleInPlayInput()
    {
        var direction = GetMovementDirection();
        if (direction != Vector2.Zero)
            OnPlayerMove?.Invoke(direction);

        if (IsPressed(Keys.Space, Buttons.A))
            OnPlayerAction?.Invoke();

        if (IsHeld(Keys.LeftShift, Buttons.B))
            OnPlayerSpeedBurst?.Invoke();
    }

    private void HandlePostPlayInput()
    {
        if (IsPressed(Keys.Enter, Buttons.A) || IsPressed(Keys.Space, Buttons.A))
            OnMenuSelect?.Invoke();
    }

    private Vector2 GetMovementDirection()
    {
        Vector2 direction = Vector2.Zero;

        if (_currentKeyboard.IsKeyDown(Keys.Up) || _currentKeyboard.IsKeyDown(Keys.W))
            direction.Y -= 1;
        if (_currentKeyboard.IsKeyDown(Keys.Down) || _currentKeyboard.IsKeyDown(Keys.S))
            direction.Y += 1;
        if (_currentKeyboard.IsKeyDown(Keys.Left) || _currentKeyboard.IsKeyDown(Keys.A))
            direction.X -= 1;
        if (_currentKeyboard.IsKeyDown(Keys.Right) || _currentKeyboard.IsKeyDown(Keys.D))
            direction.X += 1;

        if (direction == Vector2.Zero)
        {
            direction = _currentGamePad.ThumbSticks.Left;
            direction.Y *= -1;

            if (_currentGamePad.DPad.Up == ButtonState.Pressed)
                direction.Y -= 1;
            if (_currentGamePad.DPad.Down == ButtonState.Pressed)
                direction.Y += 1;
            if (_currentGamePad.DPad.Left == ButtonState.Pressed)
                direction.X -= 1;
            if (_currentGamePad.DPad.Right == ButtonState.Pressed)
                direction.X += 1;
        }

        if (direction.LengthSquared() > 1f)
            direction.Normalize();

        return direction;
    }

    private bool IsPressed(Keys key, Buttons button)
        => (_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key))
           || (_currentGamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button));

    private bool IsHeld(Keys key, Buttons button)
        => _currentKeyboard.IsKeyDown(key) || _currentGamePad.IsButtonDown(button);

    private void HandleRepeats(GameTime gameTime)
    {
        if (CurrentContext == InputContext.Menu || CurrentContext == InputContext.PlayCall)
        {
            var dir = GetMovementDirection();
            if (dir != Vector2.Zero)
            {
                _repeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_repeatTimer >= REPEAT_DELAY)
                {
                    if (dir.Y < 0) OnMenuUp?.Invoke();
                    if (dir.Y > 0) OnMenuDown?.Invoke();

                    _repeatTimer = REPEAT_DELAY - REPEAT_INTERVAL;
                }
            }
            else
            {
                _repeatTimer = 0f;
            }
        }
    }

    public bool IsKeyPressed(Keys key)
        => _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    public bool IsButtonPressed(Buttons button)
        => _currentGamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button);

    public bool IsActionPressed()
        => IsPressed(Keys.Space, Buttons.A);
}

public enum InputContext
{
    Menu,
    PlayCall,
    PreSnap,
    InPlay,
    PostPlay,
}
