using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components.Menu;
using TecmoSBGame.Events;
using TecmoSBGame.Flow;
using TecmoSBGame.Input;
using TecmoSBGame.Rendering;
using TecmoSBGame.Rendering.PlayCall;
using TecmoSBGame.Rendering.PostPlay;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.State;
using TecmoSBGame.Systems;
using TecmoSBGame.Systems.Menu;
using TecmoSBGame.Systems.PlayCall;
using TecmoSBGame.Components.PlayCall;
using TecmoSBGame.Timing;

namespace TecmoSBGame;

/// <summary>
/// Main game class for Tecmo Super Bowl MonoGame remake.
/// 
/// Design Pattern: MonoGame lifecycle with separated concerns
/// - Constructor: Graphics setup only
/// - Initialize(): Data loading, state creation, ECS world construction
/// - LoadContent(): MonoGame content (textures, fonts, sounds)
/// - Update()/Draw(): Game loop
/// </summary>
public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private World? _world;

    // Rendering
    private RenderViewport? _viewport;
    private FieldRenderer? _fieldRenderer;
    private TitleScreenRenderer? _titleRenderer;
    private MainMenuRenderer? _mainMenuRenderer;
    private ScoreboardRenderer? _scoreboardRenderer;
    private DownDistanceRenderer? _downDistanceRenderer;

    // Input/Flow
    private InputManager? _input;
    private MenuNavigationSystem? _menuNav;
    private GameFlowController? _flow;

    // UI (initialized in LoadContent, depends on MonoGame Content)
    private PlayCallUiAssets? _playCallAssets;
    private FormationSelectRenderer? _formationSelectRenderer;
    private PlaySelectRenderer? _playSelectRenderer;
    private DefensivePlaySelectRenderer? _defensiveSelectRenderer;
    private PlayDiagramRenderer? _diagramRenderer;
    private PlayCallComponent? _playCallState;

    // Game Systems (initialized in Initialize)
    private GameStateSystem? _gameStateSystem;
    private GameEvents? _events;
    private FixedTimestepRunner? _fixed;
    private MatchState? _matchState;
    private PlayState? _playState;
    private LoopState? _loopState;
    private ControlState? _controlState;

    /// <summary>
    /// Provides access to all loaded game content (YAML data, not MonoGame ContentManager).
    /// </summary>
    public GameContent GameContent { get; private set; } = null!;

    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // NES aspect ratio: 256x224 = 8:7, scaled up.
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 1120; // 224 * 5
        _graphics.SynchronizeWithVerticalRetrace = true;
    }

    /// <summary>
    /// Initialize game state, load data, and build ECS world.
    /// Called once before LoadContent.
    /// </summary>
    protected override void Initialize()
    {
        // PHASE 1: Load YAML data (no GraphicsDevice required)
        LoadGameData();

        // PHASE 2: Initialize MonoGame (creates GraphicsDevice, calls LoadContent)
        base.Initialize();

        // PHASE 3: Initialize non-MonoGame systems (renderers, input, flow)
        InitializeSystems();

        // PHASE 4: Build ECS World (depends on all above)
        BuildWorld();
    }

    /// <summary>
    /// Load all YAML game data. Must happen before any systems need it.
    /// </summary>
    private void LoadGameData()
    {
        GameContent = new GameContent(Services);
        GameContent.LoadAll();
    }

    /// <summary>
    /// Initialize systems that don't require MonoGame ContentManager.
    /// </summary>
    private void InitializeSystems()
    {
        // Renderers (use GraphicsDevice, not ContentManager)
        _viewport = new RenderViewport(GraphicsDevice);
        _fieldRenderer = new FieldRenderer(GraphicsDevice);
        _titleRenderer = new TitleScreenRenderer(GraphicsDevice);
        _mainMenuRenderer = new MainMenuRenderer(GraphicsDevice);
        _scoreboardRenderer = new ScoreboardRenderer(GraphicsDevice);
        _downDistanceRenderer = new DownDistanceRenderer(GraphicsDevice);

        // Input and Flow
        _input = new InputManager();

        _flow = new GameFlowController(seed: 0x5157);
        _flow.StateChanged += OnFlowStateChanged;

        _menuNav = new MenuNavigationSystem(_input)
        {
            Enabled = false,
        };

        _menuNav.SetItems(new[]
        {
            new MenuItemComponent(MenuItemType.Preseason, "PRESEASON", t => _flow.SelectMainMenuItem(t)),
            new MenuItemComponent(MenuItemType.Season, "SEASON", t => _flow.SelectMainMenuItem(t)),
            new MenuItemComponent(MenuItemType.ProBowl, "PRO BOWL", t => _flow.SelectMainMenuItem(t)),
            new MenuItemComponent(MenuItemType.Options, "OPTIONS", t => _flow.SelectMainMenuItem(t)),
            new MenuItemComponent(MenuItemType.Data, "DATA", t => _flow.SelectMainMenuItem(t)),
        });

        // Game State
        _events = new GameEvents();
        _matchState = new MatchState();
        _playState = new PlayState();
        _fixed = new FixedTimestepRunner(hz: 60, maxTicksPerFrame: 5);

        // Loop State (from YAML)
        var gameLoopMachine = new GameLoopMachine(GameContent.GameLoop);
        var onFieldLoopMachine = new OnFieldLoopMachine(GameContent.OnFieldLoop);
        _loopState = new LoopState(gameLoopMachine, onFieldLoopMachine);

        _controlState = new ControlState();

        _gameStateSystem = new GameStateSystem(
            _matchState,
            _playState,
            _events,
            formationData: GameContent.FormationData,
            formationSpawner: new Spawning.FormationSpawner());
    }

    /// <summary>
    /// Build the ECS World with all systems.
    /// Must be called after InitializeSystems() so all dependencies exist.
    /// </summary>
    private void BuildWorld()
    {
        if (_events == null || _matchState == null || _playState == null || 
            _loopState == null || _controlState == null || _gameStateSystem == null)
        {
            throw new InvalidOperationException("Cannot build world: systems not initialized");
        }

        _world = new WorldBuilder()
            // Route runners
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new ManCoverageSystem(_events, _playState))
            .AddSystem(new ZoneCoverageSystem(_events, _playState))
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            // Pre-snap
            .AddSystem(new PreSnapSystem(_loopState, _matchState, _playState))
            .AddSystem(new PreSnapBallPlacementSystem(_loopState, _matchState, _playState))
            .AddSystem(new PlayCallSystem(
                _loopState,
                _playState,
                _events,
                GameContent.FormationData,
                GameContent.PlayList,
                GameContent.DefensePlays))
            // Input/Control
            .AddSystem(new PlayerControlSystem(_controlState, _loopState, enableInput: true))
            .AddSystem(new InputSystem(_loopState))
            // Actions
            .AddSystem(new ActionResolutionSystem(_events, _matchState, _playState))
            .AddSystem(new SnapResolutionSystem(_events, _matchState, _playState))
            .AddSystem(new PenaltySystem(_events, _matchState, _playState))
            // Blocking/Contact
            .AddSystem(new BlockerAISystem(_events, _loopState, _playState))
            .AddSystem(new CollisionContactSystem(_events, _loopState))
            .AddSystem(new EngagementSystem(_events))
            .AddSystem(new TackleInterruptSystem(_events))
            .AddSystem(new TackleResolutionSystem(_events, _matchState, _playState))
            .AddSystem(new BehaviorStackSystem())
            // QB/Pass
            .AddSystem(new QbDropbackSystem(_events, _matchState, _playState))
            .AddSystem(new ReadProgressionSystem(_events, _matchState, _playState))
            .AddSystem(new PassFlightStartSystem(_events, _playState))
            .AddSystem(new PassFlightCompleteSystem(_events, _playState))
            // Ball
            .AddSystem(new BallPhysicsSystem())
            .AddSystem(new BallBoundsSystem(_events, _matchState, _playState))
            // Game State
            .AddSystem(_gameStateSystem)
            .AddSystem(new WhistleOnTackleSystem(_events))
            .AddSystem(new FumbleOnTackleWhistleSystem(_events, _playState))
            .AddSystem(new FumbleResolutionSystem(_events, _playState))
            .AddSystem(new LooseBallPickupSystem(_events, _playState))
            // Clock/Rules
            .AddSystem(new GameClockSystem(_events, _matchState, _playState, _loopState))
            .AddSystem(new PlayEndSystem(_events, _matchState, _playState))
            .AddSystem(new NextPlayResetSystem(_events, _matchState, _playState))
            .AddSystem(new DownDistanceSystem(_events, _matchState))
            .AddSystem(new KickoffAfterScoreSystem(_events, _matchState, _playState))
            // HUD
            .AddSystem(new HudSystem(_matchState, _playState, _flow))
            // Rendering
            .AddSystem(new RenderingSystem(_spriteBatch, GraphicsDevice))
            .Build();

        // Create playcall entity
        _playCallState = new PlayCallComponent();
        var playcallEntity = _world.CreateEntity();
        playcallEntity.Attach(_playCallState);

        // Spawn initial scenario
        _gameStateSystem.SpawnKickoffScenario(_world);
    }

    /// <summary>
    /// Load MonoGame content (textures, fonts, sounds).
    /// Called by base.Initialize() after GraphicsDevice is created.
    /// </summary>
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);

        // Initialize font system for UI text rendering
        FontSystem.Instance.Load(Content);

        // Load fonts
        var playcallFont = Content.Load<SpriteFont>("Fonts/Playcall");
        _playCallAssets = new PlayCallUiAssets(playcallFont, GraphicsDevice);

        // Initialize playcall renderers
        if (GameContent?.FormationData == null)
            throw new InvalidOperationException("FormationData not loaded. Check YAML content files.");

        _formationSelectRenderer = new FormationSelectRenderer(_playCallAssets, GameContent.FormationData);
        _playSelectRenderer = new PlaySelectRenderer(_playCallAssets);
        _defensiveSelectRenderer = new DefensivePlaySelectRenderer(_playCallAssets);
        _diagramRenderer = new PlayDiagramRenderer(_playCallAssets);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_flow is null || _input is null)
        {
            base.Update(gameTime);
            return;
        }

        _input.SetContext(_flow.State == GameFlowState.OnField ? InputContext.InPlay : InputContext.Menu);
        _input.Update(gameTime);

        bool startPressed = _input.IsKeyPressed(Keys.Enter) || _input.IsButtonPressed(Buttons.A);
        bool leftPressed = _input.IsKeyPressed(Keys.Left) || _input.IsButtonPressed(Buttons.DPadLeft);
        bool rightPressed = _input.IsKeyPressed(Keys.Right) || _input.IsButtonPressed(Buttons.DPadRight);
        bool upPressed = _input.IsKeyPressed(Keys.Up) || _input.IsButtonPressed(Buttons.DPadUp);
        bool downPressed = _input.IsKeyPressed(Keys.Down) || _input.IsButtonPressed(Buttons.DPadDown);

        _flow.UpdateUiInput(startPressed, leftPressed, rightPressed, upPressed, downPressed);

        // Advance simulation
        if (_fixed is not null && _events is not null && _world is not null)
        {
            _fixed.Advance(gameTime.ElapsedGameTime, fixedGameTime =>
            {
                _events.BeginTick();
                _world.Update(fixedGameTime);
            });
        }

        base.Update(gameTime);
    }

    private void OnFlowStateChanged(GameFlowState prev, GameFlowState next)
    {
        Console.WriteLine($"[flow] {prev} -> {next}");

        if (_menuNav is not null)
            _menuNav.Enabled = next == GameFlowState.MainMenu;

        if (next == GameFlowState.TeamSelect && _matchState is not null && _flow is not null)
        {
            _flow.ConfirmTeamSelection(_matchState);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null)
        {
            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            effect: null,
            transformMatrix: _viewport?.ScaleMatrix);

        // Draw based on current flow state
        if (_flow is not null && _flow.State == GameFlowState.Title)
        {
            _titleRenderer?.Draw(_spriteBatch, (float)gameTime.TotalGameTime.TotalSeconds);
        }
        else if (_flow is not null && _flow.State == GameFlowState.MainMenu)
        {
            _mainMenuRenderer?.Draw(_spriteBatch, _menuNav?.SelectedIndex ?? 0);
        }
        else
        {
            // In-game rendering
            _fieldRenderer?.Draw(_spriteBatch);
            _world?.Draw(gameTime);

            // HUD (scoreboard, down/distance)
            if (_matchState is not null)
            {
                _scoreboardRenderer?.Draw(_spriteBatch, _matchState);
                _downDistanceRenderer?.Draw(_spriteBatch, _matchState);
            }

            // Debug info
            DrawDebugInfo(_spriteBatch);

            // Playcall overlay
            if (_playCallState is not null && _playCallState.Visible)
            {
                var formationArea = new Rectangle(6, 6, 120, 212);
                var playsArea = new Rectangle(130, 6, 120, 110);

                _formationSelectRenderer?.Draw(_spriteBatch, formationArea, _playCallState);
                _playSelectRenderer?.Draw(_spriteBatch, playsArea, _playCallState);

                if (_playCallState.Step == PlayCallStep.Defense)
                    _defensiveSelectRenderer?.Draw(_spriteBatch, playsArea, _playCallState);
            }
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    /// <summary>
    /// Draw debug info showing entity count and game state.
    /// </summary>
    private void DrawDebugInfo(SpriteBatch sb)
    {
        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font == null) return;

        var sb_debug = $"DEBUG: Flow={_flow?.State}";
        var entities = _world?.EntityCount.ToString() ?? "0";
        var sb_entities = $"Entities: {entities}";

        sb.DrawString(font, sb_debug, new Vector2(4, 200), Color.Yellow);
        sb.DrawString(font, sb_entities, new Vector2(4, 212), Color.Yellow);
    }
}
