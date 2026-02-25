using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TecmoSBGame.Input;

/// <summary>
/// Centralized input manager that handles different game contexts.
/// Routes input based on current game state (menu, play call, pre-snap, in-play).
/// </summary>
public class InputManager
{
    // Current state
    public InputContext CurrentContext { get; private set; } = InputContext.Menu;
    
    // Input states
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private GamePadState _currentGamePad;
    private GamePadState _previousGamePad;
    
    // Repeat handling
    private float _repeatTimer = 0f;
    private const float REPEAT_DELAY = 0.3f;
    private const float REPEAT_INTERVAL = 0.1f;
    
    // Events for different contexts
    public event Action? OnMenuUp;
    public event Action? OnMenuDown;
    public event Action? OnMenuSelect;
    public event Action? OnMenuBack;
    
    public event Action? OnPlayCallUp;
    public event Action? OnPlayCallDown;
    public event Action? OnPlayCallLeft;
    public event Action? OnPlayCallRight;
    public event Action? OnPlayCallSelect;
    public event Action? OnPlayCallAudible;
    
    public event Action? OnPreSnapMotionLeft;
    public event Action? OnPreSnapMotionRight;
    public event Action? OnPreSnapHotRoute;
    public event Action? OnPreSnapSnap;
    
    public event Action<Vector2>? OnPlayerMove;
    public event Action? OnPlayerAction;
    public event Action? OnPlayerSpeedBurst;
    
    public event Action? OnPause;
    
    public void Update(GameTime gameTime)
    {
        // Update input states
        _previousKeyboard = _currentKeyboard;
        _previousGamePad = _currentGamePad;
        _currentKeyboard = Keyboard.GetState();
        _currentGamePad = GamePad.GetState(PlayerIndex.One);
        
        // Handle repeat for held keys
        HandleRepeats(gameTime);
        
        // Route input based on context
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
        
        // Global pause
        if (IsPressed(Keys.Escape, Buttons.Start))
        {
            OnPause?.Invoke();
        }
    }
    
    public void SetContext(InputContext context)
    {
        CurrentContext = context;
        _repeatTimer = 0f;
    }
    
    // Context-specific handlers
    
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
        // Get movement direction
        Vector2 direction = GetMovementDirection();
        if (direction != Vector2.Zero)
        {
            OnPlayerMove?.Invoke(direction);
        }
        
        // Action button (dive/tackle/break tackle)
        if (IsPressed(Keys.Space, Buttons.A))
        {
            OnPlayerAction?.Invoke();
        }
        
        // Speed burst
        if (IsHeld(Keys.LeftShift, Buttons.B))
        {
            OnPlayerSpeedBurst?.Invoke();
        }
    }
    
    private void HandlePostPlayInput()
    {
        // Minimal input - just continue/next
        if (IsPressed(Keys.Enter, Buttons.A) || IsPressed(Keys.Space, Buttons.A))
        {
            OnMenuSelect?.Invoke();
        }
    }
    
    // Helper methods
    
    private Vector2 GetMovementDirection()
    {
        Vector2 direction = Vector2.Zero;
        
        // Keyboard
        if (_currentKeyboard.IsKeyDown(Keys.Up) || _currentKeyboard.IsKeyDown(Keys.W))
            direction.Y -= 1;
        if (_currentKeyboard.IsKeyDown(Keys.Down) || _currentKeyboard.IsKeyDown(Keys.S))
            direction.Y += 1;
        if (_currentKeyboard.IsKeyDown(Keys.Left) || _currentKeyboard.IsKeyDown(Keys.A))
            direction.X -= 1;
        if (_currentKeyboard.IsKeyDown(Keys.Right) || _currentKeyboard.IsKeyDown(Keys.D))
            direction.X += 1;
        
        // GamePad
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
        
        // Normalize
        if (direction.LengthSquared() > 1f)
            direction.Normalize();
        
        return direction;
    }
    
    private bool IsPressed(Keys key, Buttons button)
    {
        return (_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key)) ||
               (_currentGamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button));
    }
    
    private bool IsHeld(Keys key, Buttons button)
    {
        return _currentKeyboard.IsKeyDown(key) || _currentGamePad.IsButtonDown(button);
    }
    
    private void HandleRepeats(GameTime gameTime)
    {
        // For held directional keys in menu/play call contexts
        if (CurrentContext == InputContext.Menu || CurrentContext == InputContext.PlayCall)
        {
            Vector2 dir = GetMovementDirection();
            if (dir != Vector2.Zero)
            {
                _repeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                if (_repeatTimer >= REPEAT_DELAY)
                {
                    // Fire repeat event
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
    
    // Utility methods for checking current input
    
    public bool IsKeyPressed(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }
    
    public bool IsButtonPressed(Buttons button)
    {
        return _currentGamePad.IsButtonDown(button) && _previousGamePad.IsButtonUp(button);
    }
    
    public bool IsActionPressed()
    {
        return IsPressed(Keys.Space, Buttons.A);
    }
}

/// <summary>
/// Different contexts for input handling.
/// </summary>
public enum InputContext
{
    Menu,
    PlayCall,
    PreSnap,
    InPlay,
    PostPlay
}
