using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Entities;
using TecmoSBGame.Events;
using TecmoSBGame.Rendering;
using TecmoSB;
using TecmoSBGame.State;
using TecmoSBGame.Systems;
using TecmoSBGame.Timing;

namespace TecmoSBGame;

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private World? _world;
    private RenderViewport? _viewport;
    private FieldRenderer? _fieldRenderer;
    private GameStateSystem? _gameStateSystem;
    private GameEvents? _events;
    private FixedTimestepRunner? _fixed;
    private MatchState? _matchState;
    private PlayState? _playState;
    private LoopState? _loopState;
    private ControlState? _controlState;

    /// <summary>
    /// Provides access to all loaded game content.
    /// </summary>
    public GameContent GameContent { get; private set; } = null!;

    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // NES aspect ratio: 256x224 = 8:7, scaled up
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 1120; // 224 * 5
        _graphics.SynchronizeWithVerticalRetrace = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Initialize rendering viewport
        _viewport = new RenderViewport(GraphicsDevice);
        _fieldRenderer = new FieldRenderer(GraphicsDevice);

        // Load all YAML content at startup
        GameContent = new GameContent(Services);
        GameContent.LoadAll();

        // Event bus is created once and cleared once per simulation tick by the fixed-step driver.
        _events = new GameEvents();

        // Pure data model of match-level state (shared across systems).
        _matchState = new MatchState();

        // Pure data model of play-level state (shared across systems).
        _playState = new PlayState();

        // Fixed 60Hz simulation (NES-style). Rendering remains variable.
        _fixed = new FixedTimestepRunner(hz: 60, maxTicksPerFrame: 5);

        // Instantiate authoritative YAML loop machines and shared loop state.
        var gameLoopMachine = new GameLoopMachine(GameContent.GameLoop);
        var onFieldLoopMachine = new OnFieldLoopMachine(GameContent.OnFieldLoop);
        _loopState = new LoopState(gameLoopMachine, onFieldLoopMachine);

        // Small shared service for "which entity currently receives player input".
        _controlState = new ControlState();

        // Create game state system (used by ECS world)
        if (_playState is null)
            throw new InvalidOperationException("PlayState was not initialized.");

        _gameStateSystem = new GameStateSystem(_matchState, _playState, _events);
    }

    protected override void LoadContent()
    {
        // MonoGame lifecycle: GraphicsDevice is ready here, so create SpriteBatch here.
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);

        // Build ECS world after SpriteBatch exists (RenderingSystem requires it).
        if (_world is null)
        {
            if (_gameStateSystem is null)
                throw new InvalidOperationException("Game state system was not initialized.");

            if (_events is null)
                throw new InvalidOperationException("GameEvents was not initialized.");

            if (_loopState is null)
                throw new InvalidOperationException("LoopState was not initialized.");

            if (_controlState is null)
                throw new InvalidOperationException("ControlState was not initialized.");

            if (_matchState is null)
                throw new InvalidOperationException("MatchState was not initialized.");
            if (_playState is null)
                throw new InvalidOperationException("PlayState was not initialized.");

            _world = new WorldBuilder()
                .AddSystem(new MovementSystem())
                // Selection runs before input so the tick's movement is applied to the chosen entity.
                .AddSystem(new PlayerControlSystem(_controlState, _loopState, enableInput: true))
                .AddSystem(new InputSystem(_loopState))
                .AddSystem(new ActionResolutionSystem(_events, _matchState, _playState))
                .AddSystem(new PassFlightStartSystem(_events, _playState))
                .AddSystem(_gameStateSystem)
                .AddSystem(new BallPhysicsSystem())
                .AddSystem(new PassFlightCompleteSystem(_events, _playState))
                .AddSystem(new BallBoundsSystem(_events, _matchState, _playState))
                .AddSystem(new WhistleOnTackleSystem(_events))
                // TEMP: fumbles triggered off tackle whistle until tackle rules resolve.
                .AddSystem(new FumbleOnTackleWhistleSystem(_events, _playState))
                .AddSystem(new FumbleResolutionSystem(_events, _playState))
                .AddSystem(new LooseBallPickupSystem(_events, _playState))
                // Loop driver runs late so it can observe events published earlier in the tick.
                .AddSystem(new LoopMachineSystem(_loopState, _events))
                .AddSystem(new RenderingSystem(_spriteBatch, GraphicsDevice))
                .Build();

            // Spawn the kickoff scenario
            _gameStateSystem.SpawnKickoffScenario(_world);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (_fixed is null || _events is null || _world is null)
        {
            base.Update(gameTime);
            return;
        }

        // Accumulate real time (variable) and advance the simulation at a fixed 60Hz.
        // We may execute 0..N ticks per frame depending on render/update cadence.
        _fixed.Advance(gameTime.ElapsedGameTime, fixedGameTime =>
        {
            // One clear per simulation tick keeps event processing deterministic.
            _events.BeginTick();
            _world.Update(fixedGameTime);
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));

        if (_spriteBatch is null)
        {
            base.Draw(gameTime);
            return;
        }

        // Begin rendering with viewport scale
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            effect: null,
            transformMatrix: _viewport?.ScaleMatrix);

        // Draw field
        _fieldRenderer?.Draw(_spriteBatch);

        // Draw ECS entities inside the same batch (RenderingSystem must not Begin/End).
        _world?.Draw(gameTime);

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
