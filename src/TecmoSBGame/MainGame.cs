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

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private World? _world;

    private RenderViewport? _viewport;
    private FieldRenderer? _fieldRenderer;

    private TitleScreenRenderer? _titleRenderer;
    private MainMenuRenderer? _mainMenuRenderer;

    private InputManager? _input;
    private MenuNavigationSystem? _menuNav;

    private GameFlowController? _flow;

    // Playcall UI (pre-snap)
    private PlayCallUiAssets? _playCallAssets;
    private FormationSelectRenderer? _formationSelectRenderer;
    private PlaySelectRenderer? _playSelectRenderer;
    private DefensivePlaySelectRenderer? _defensiveSelectRenderer;
    private PlayDiagramRenderer? _diagramRenderer;
    private PlayCallComponent? _playCallState;

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

        // NES aspect ratio: 256x224 = 8:7, scaled up.
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 1120; // 224 * 5
        _graphics.SynchronizeWithVerticalRetrace = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        _viewport = new RenderViewport(GraphicsDevice);
        _fieldRenderer = new FieldRenderer(GraphicsDevice);
        _titleRenderer = new TitleScreenRenderer(GraphicsDevice);
        _mainMenuRenderer = new MainMenuRenderer(GraphicsDevice);

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

        // Load all YAML content at startup.
        GameContent = new GameContent(Services);
        GameContent.LoadAll();

        _events = new GameEvents();

        _matchState = new MatchState();
        _playState = new PlayState();

        _fixed = new FixedTimestepRunner(hz: 60, maxTicksPerFrame: 5);

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

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);

        // Playcall UI assets (SpriteFont + 1x1 pixel).
        // NOTE: requires Content/Content.mgcb to include Fonts/Playcall.spritefont.
        var playcallFont = Content.Load<SpriteFont>("Fonts/Playcall");
        _playCallAssets = new PlayCallUiAssets(playcallFont, GraphicsDevice);
        _formationSelectRenderer = new FormationSelectRenderer(_playCallAssets, GameContent.FormationData);
        _playSelectRenderer = new PlaySelectRenderer(_playCallAssets);
        _defensiveSelectRenderer = new DefensivePlaySelectRenderer(_playCallAssets);
        _diagramRenderer = new PlayDiagramRenderer(_playCallAssets);

        if (_world is not null)
            return;

        if (_spriteBatch is null || _events is null || _loopState is null || _controlState is null || _gameStateSystem is null)
            throw new InvalidOperationException("MainGame was not initialized.");

        if (_matchState is null || _playState is null)
            throw new InvalidOperationException("Match/Play state was not initialized.");

        _world = new WorldBuilder()
            // Drive Behavior targets for route runners BEFORE MovementSystem reads Behavior.
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new MovementSystem())
            .AddSystem(new SpeedModifierSystem())
            // Pre-snap deterministic placement (scrimmage plays).
            .AddSystem(new PreSnapSystem(_loopState, _matchState, _playState))
            .AddSystem(new PreSnapBallPlacementSystem(_loopState, _matchState, _playState))
            // Pre-snap playcall selection UI.
            .AddSystem(new PlayCallSystem(
                _loopState,
                _playState,
                _events,
                GameContent.FormationData,
                GameContent.PlayList,
                GameContent.DefensePlays))
            // Selection runs before input so the tick's movement is applied to the chosen entity.
            .AddSystem(new PlayerControlSystem(_controlState, _loopState, enableInput: true))
            .AddSystem(new InputSystem(_loopState))
            .AddSystem(new ActionResolutionSystem(_events, _matchState, _playState))
            .AddSystem(new SnapResolutionSystem(_events, _matchState, _playState))
            // Penalties are scaffolded but default to Off (no behavior changes).
            .AddSystem(new PenaltySystem(_events, _matchState, _playState))
            // Blocking AI drives blocker target selection/movement; EngagementSystem consumes BlockContactEvent.
            .AddSystem(new BlockerAISystem(_events, _loopState, _playState))
            .AddSystem(new CollisionContactSystem(_events, _loopState))
            .AddSystem(new EngagementSystem(_events))
            .AddSystem(new TackleInterruptSystem(_events))
            .AddSystem(new TackleResolutionSystem(_events, _matchState, _playState))
            .AddSystem(new BehaviorStackSystem())
            // QB AI (dropback + reads) runs before pass flight start.
            .AddSystem(new QbDropbackSystem(_events, _matchState, _playState))
            .AddSystem(new ReadProgressionSystem(_events, _matchState, _playState))
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
            // Authoritative play-end aggregation (reads whistles, finalizes play state, emits PlayEndedEvent).
            .AddSystem(new PlayEndSystem(_events, _matchState, _playState, log: true))
            // Rules/refereeing: down & distance progression, possession, spotting (observes PlayEndedEvent).
            .AddSystem(new DownDistanceSystem(_events, _matchState, log: true))
            // Deterministic score->kickoff transition.
            .AddSystem(new KickoffAfterScoreSystem(_events, _matchState, _playState, log: true))
            // Deterministic PostPlay -> PreSnap reset for normal (non-scoring) plays.
            .AddSystem(new NextPlayResetSystem(_events, _matchState, _playState, _loopState, log: true))
            // Loop driver runs late so it can observe events published earlier in the tick.
            .AddSystem(new LoopMachineSystem(_loopState, _events))
            // Deterministic game clock (runs off loop state).
            .AddSystem(new GameClockSystem(_events, _matchState, _playState, _loopState, log: true))
            .AddSystem(new ContactDebugLogSystem(_events))
            .AddSystem(new RenderingSystem(_spriteBatch, GraphicsDevice))
            .Build();

        // Create a singleton playcall UI entity.
        var playcallEntity = _world.CreateEntity();
        _playCallState = new PlayCallComponent();
        playcallEntity.Attach(_playCallState);

        // Spawn an initial kickoff scenario so the field sim has something to run when we reach gameplay.
        _gameStateSystem.SpawnKickoffScenario(_world);
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

        // Advance simulation continuously for now (gameplay systems read keyboard directly).
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

        // When entering TeamSelect, stash selection to match state (for future wiring).
        if (next == GameFlowState.TeamSelect && _matchState is not null && _flow is not null)
        {
            // TODO: handle SelectedMainMenuItem in match setup once game modes are implemented.
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
            _fieldRenderer?.Draw(_spriteBatch);
            _world?.Draw(gameTime);

            // Pre-snap playcall UI overlay.
            if (_playCallState is not null && _playCallState.Visible)
            {
                // NES virtual resolution (256x224).
                var formationArea = new Rectangle(6, 6, 120, 212);
                var playsArea = new Rectangle(130, 6, 120, 110);
                var diagramArea = new Rectangle(130, 120, 120, 98);

                _formationSelectRenderer?.Draw(_spriteBatch, formationArea, _playCallState);
                _playSelectRenderer?.Draw(_spriteBatch, playsArea, _playCallState);

                // When in defense step, the plays pane becomes a defense call grid.
                if (_playCallState.Step == PlayCallStep.Defense)
                    _defensiveSelectRenderer?.Draw(_spriteBatch, playsArea, _playCallState);

                _diagramRenderer?.Draw(_spriteBatch, diagramArea, _playCallState);
            }
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
