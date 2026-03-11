using System;
using System.Collections.Generic;
using System.Linq;
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
using TecmoSBGame.Components;
using TecmoSBGame.Components.PlayCall;
using TecmoSBGame.Diagnostics;
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
    private RenderResources? _renderResources;
    private FieldRenderer? _fieldRenderer;
    private TitleScreenRenderer? _titleRenderer;
    private MainMenuRenderer? _mainMenuRenderer;
    private ScoreboardRenderer? _scoreboardRenderer;
    private DownDistanceRenderer? _downDistanceRenderer;

    // Debug rendering toggles
    private bool _showEntityLabels = true;
    private RenderingSystem? _renderingSystem;

    // Debug sim toggles
    private FormationScriptSystem? _formationScriptSystem;
    private bool _debugScriptLog;
    private bool _debugHudLog;

    // Flow input debounce: prevent a single button press from skipping multiple menu states.
    private float _ignoreStartSeconds;

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

    // Dev shortcut: auto-playcall orchestration (spawn roster + apply selected play) until real playcall system exists.
    private GameStateSystem.KickoffScenarioIds? _kickoffScenario;
    private bool _scrimmageRosterInitialized;
    private readonly List<int> _scrimmageOffenseIds = new(capacity: 11);
    private readonly List<int> _scrimmageDefenseIds = new(capacity: 11);
    private int _lastScrimmagePlaySpawnedId = -1;

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
        // Capture all Console output to a file (still prints to console).
        // This is critical when debug logging is too verbose to paste from a terminal.
        GameLog.InstallConsoleTee();

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
        _renderResources = new RenderResources(GraphicsDevice);
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
            formationSpawner: new Spawning.FormationSpawner(),
            playList: GameContent.PlayList,
            defensePlays: GameContent.DefensePlays);
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

        if (_spriteBatch is null || _renderResources is null)
            throw new InvalidOperationException("Cannot build world: missing SpriteBatch or RenderResources");

        var spriteRegistry = new TecmoSBGame.Rendering.Sprites.SpriteRegistry();
        if (GameContent.SpriteManifest is not null)
            spriteRegistry.LoadFromManifest(Content, GameContent.SpriteManifest);

        _renderingSystem = new RenderingSystem(_spriteBatch, _renderResources.Pixel, spriteRegistry)
        {
            ShowLabels = _showEntityLabels,
        };

        _formationScriptSystem = new FormationScriptSystem(_playState)
        {
            DebugLog = _debugScriptLog,
        };

        _world = new WorldBuilder()
            // Route runners
            .AddSystem(new RouteFollowSystem())
            .AddSystem(new ManCoverageSystem(_events, _playState))
            .AddSystem(new ZoneCoverageSystem(_events, _playState))
            // Execute play scripts (PlayData YAML) to drive behavior.
            .AddSystem(new PlayScriptSystem(_playState, _matchState, _controlState))
            // Execute Tecmo-style formation scripts (YAML commands) to drive behavior.
            .AddSystem(_formationScriptSystem)
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
            .AddSystem(new CollisionContactSystem(_events, _loopState, _playState))
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
            .AddSystem(new DownDistanceSystem(_events, _matchState))
            .AddSystem(new NextPlayResetSystem(_events, _matchState, _playState, _loopState))
            .AddSystem(new KickoffAfterScoreSystem(_events, _matchState, _playState))
            // HUD
            .AddSystem(new HudSystem(_matchState, _playState, _flow))
            // Rendering
            .AddSystem(_renderingSystem)
            .Build();

        // Create playcall entity
        _playCallState = new PlayCallComponent();
        var playcallEntity = _world.CreateEntity();
        playcallEntity.Attach(_playCallState);

        // Spawn initial scenario
        _kickoffScenario = _gameStateSystem.SpawnKickoffScenario(_world);
        _scrimmageRosterInitialized = false;
        _lastScrimmagePlaySpawnedId = -1;
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

        // Start/confirm (debounced so we don't skip multiple states on one press)
        _ignoreStartSeconds = Math.Max(0f, _ignoreStartSeconds - (float)gameTime.ElapsedGameTime.TotalSeconds);
        bool startPressedRaw = _input.IsKeyPressed(Keys.Enter) || _input.IsButtonPressed(Buttons.A);
        bool startPressed = startPressedRaw && _ignoreStartSeconds <= 0f;

        // IMPORTANT: during a live kickoff, A/Enter is used for gameplay input as well.
        // Do not allow it to skip the entire kickoff flow into OnField mid-return.
        if (_flow.State == GameFlowState.Kickoff && _gameStateSystem is not null)
        {
            if (_gameStateSystem.CurrentPhase != TecmoSBGame.Systems.GamePhase.End)
                startPressed = false;
        }
        bool leftPressed = _input.IsKeyPressed(Keys.Left) || _input.IsButtonPressed(Buttons.DPadLeft);
        bool rightPressed = _input.IsKeyPressed(Keys.Right) || _input.IsButtonPressed(Buttons.DPadRight);
        bool upPressed = _input.IsKeyPressed(Keys.Up) || _input.IsButtonPressed(Buttons.DPadUp);
        bool downPressed = _input.IsKeyPressed(Keys.Down) || _input.IsButtonPressed(Buttons.DPadDown);

        _flow.UpdateUiInput(startPressed, leftPressed, rightPressed, upPressed, downPressed);

        // Debug toggles
        if (_input.IsKeyPressed(Keys.F1))
        {
            _showEntityLabels = !_showEntityLabels;
            if (_renderingSystem is not null)
                _renderingSystem.ShowLabels = _showEntityLabels;
        }

        // F2: toggle formation-script interpreter logging
        if (_input.IsKeyPressed(Keys.F2))
        {
            _debugScriptLog = !_debugScriptLog;
            if (_formationScriptSystem is not null)
                _formationScriptSystem.DebugLog = _debugScriptLog;
            Console.WriteLine($"[debug] scriptLog={_debugScriptLog}");
        }

        // F3: toggle periodic HUD/state logging
        if (_input.IsKeyPressed(Keys.F3))
        {
            _debugHudLog = !_debugHudLog;
            Console.WriteLine($"[debug] hudLog={_debugHudLog}");
        }

        // Advance simulation only when we're in gameplay flow states.
        // This prevents the world from progressing (whistles, play-end, etc.) while the user is in menus.
        var simEnabled = _flow is not null && _flow.State is GameFlowState.Kickoff or GameFlowState.OnField or GameFlowState.PostPlay;
        if (simEnabled && _fixed is not null && _events is not null && _world is not null)
        {
            _fixed.Advance(gameTime.ElapsedGameTime, fixedGameTime =>
            {
                _events.BeginTick();
                _world.Update(fixedGameTime);

                // Apply manual play selection (PlayCallSystem emits PlaySelectedEvent).
                _events.Drain<TecmoSBGame.Events.PlaySelectedEvent>(ApplyPlaySelected);

                // Dev shortcut: auto-select and spawn a deterministic scrimmage play every down.
                AutoPlaycallTick();

                if (_debugHudLog && _gameStateSystem is not null && _playState is not null && _matchState is not null)
                {
                    // Lightweight periodic log (roughly 2x/sec at 60hz fixed step)
                    if (((int)(_playState.PlayElapsedSeconds * 2)) != (int)((_playState.PlayElapsedSeconds - (float)fixedGameTime.ElapsedGameTime.TotalSeconds) * 2))
                    {
                        Console.WriteLine($"[hud] flow={_flow?.State} phase={_gameStateSystem.CurrentPhase} ballState={_playState.BallState} owner={_playState.BallOwnerEntityId?.ToString() ?? "none"} playPhase={_playState.Phase} down={_matchState.Down} spot={_matchState.BallSpot}");
                    }
                }
            });
        }

        base.Update(gameTime);
    }

    private void ApplyPlaySelected(TecmoSBGame.Events.PlaySelectedEvent e)
    {
        if (_world is null || _events is null || _matchState is null || _playState is null)
            return;

        // Only accept selections during scrimmage pre-snap.
        if (_playState.Phase != TecmoSBGame.State.PlayPhase.PreSnap)
            return;

        // Make sure scrimmage roster exists.
        EnsureScrimmageRoster(e.OffensiveFormationId);

        var defId = GameContent.DefensePlays?.DefensiveExecutions?.FirstOrDefault()?.Id ?? "DEFENSIVE_EXECUTION_1";

        // Apply play assignments for this down.
        var spawner = new Spawning.PlaySpawner();
        var spawned = spawner.Spawn(
            world: _world,
            playList: GameContent.PlayList,
            defensePlays: GameContent.DefensePlays,
            offenseEntityIds: _scrimmageOffenseIds,
            defenseEntityIds: _scrimmageDefenseIds,
            selectedOffensivePlay: new TecmoSB.PlayEntry(
                Name: e.OffensivePlayName,
                Slot: e.OffensivePlaySlot,
                Formation: e.OffensiveFormationId,
                PlayNumbers: new[] { e.OffensivePlayNumber },
                Defense: Array.Empty<string>()
            ),
            selectedDefensiveCallId: defId);

        ApplyPlayDataScripts(offensivePlayNumber: e.OffensivePlayNumber);

        // Hide playcall for this down.
        _playState.PlayCallLockedIn = true;

        Console.WriteLine($"[playcall] applied playId={_playState.PlayId} play_number={e.OffensivePlayNumber} def={spawned.DefensiveCallId}");
    }

    private void AutoPlaycallTick()
    {
        if (_world is null || _events is null || _matchState is null || _playState is null || _playCallState is null)
            return;

        if (!_playCallState.Visible)
            return;

        if (!_playState.AutoPlaycallEnabled)
            return;

        // Only once per play.
        if (_lastScrimmagePlaySpawnedId == _playState.PlayId)
            return;

        var off = _playCallState.SelectedPlay;
        if (off is null)
            return;

        var defId = _playCallState.SelectedDefenseId;
        if (string.IsNullOrWhiteSpace(defId))
            defId = GameContent.DefensePlays?.DefensiveExecutions?.FirstOrDefault()?.Id ?? "DEFENSIVE_EXECUTION_1";

        EnsureScrimmageRoster(off.Formation ?? _playCallState.SelectedFormationId);

        // Apply play assignments for this down.
        var spawner = new Spawning.PlaySpawner();
        var spawned = spawner.Spawn(
            world: _world,
            playList: GameContent.PlayList,
            defensePlays: GameContent.DefensePlays,
            offenseEntityIds: _scrimmageOffenseIds,
            defenseEntityIds: _scrimmageDefenseIds,
            selectedOffensivePlay: off,
            selectedDefensiveCallId: defId);

        var offNum = off.PlayNumbers is not null && off.PlayNumbers.Count > 0 ? off.PlayNumbers[0] : 0;

        // Attach play-data YAML scripts (ROM-style player reactions) when available.
        ApplyPlayDataScripts(offensivePlayNumber: offNum);

        Console.WriteLine($"[autoplaycall] playId={_playState.PlayId} off=\"{off.Name}\" ({off.Formation}/{off.Slot}) play_number={offNum} def={spawned.DefensiveCallId}");
        _lastScrimmagePlaySpawnedId = _playState.PlayId;
    }

    private void ApplyPlayDataScripts(int offensivePlayNumber)
    {
        if (_world is null)
            return;

        if (offensivePlayNumber <= 0)
            return;

        var def = GameContent.PlayData.Plays.FirstOrDefault(p => p.PlayNumber == offensivePlayNumber);
        if (def is null)
            return;

        var reactionById = GameContent.PlayData.PlayerReactions.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

        int attached = 0;

        void AttachTo(int entityId, string? reactionId)
        {
            if (string.IsNullOrWhiteSpace(reactionId))
                return;

            if (!reactionById.TryGetValue(reactionId, out var reaction))
                return;

            var ops = Spawning.PlayScriptCompiler.Compile(reaction);
            if (ops.Count == 0)
                return;

            var e = _world!.GetEntity(entityId);

            // Replace by attaching a fresh component each play; if already present, just reset state.
            if (!e.Has<PlayScriptComponent>())
                e.Attach(new PlayScriptComponent(reaction.Id, ops));
            else
            {
                var s = e.Get<PlayScriptComponent>();
                s.Ip = 0;
                s.WaitSeconds = 0;
            }

            attached++;
        }

        // Offense by slot.
        foreach (var id in _scrimmageOffenseIds)
        {
            var e = _world.GetEntity(id);
            if (!e.Has<PlayerRoleComponent>())
                continue;

            var slot = (e.Get<PlayerRoleComponent>().Slot ?? string.Empty).Trim();
            if (def.Offense.TryGetValue(slot, out var reactionId))
                AttachTo(id, reactionId);
        }

        // Defense by slot.
        foreach (var id in _scrimmageDefenseIds)
        {
            var e = _world.GetEntity(id);
            if (!e.Has<PlayerRoleComponent>())
                continue;

            var slot = (e.Get<PlayerRoleComponent>().Slot ?? string.Empty).Trim();
            if (def.Defense.TryGetValue(slot, out var reactionId))
                AttachTo(id, reactionId);
        }

        if (attached > 0)
            Console.WriteLine($"[playdata] play_number={offensivePlayNumber} scripts={attached}");
    }

    private void EnsureScrimmageRoster(string offenseFormationId)
    {
        if (_scrimmageRosterInitialized)
            return;

        // Hide kickoff entities to prevent immediate collision/tackle loops.
        if (_kickoffScenario is not null)
        {
            foreach (var id in _kickoffScenario.Value.AllEntityIds)
            {
                if (id == _kickoffScenario.Value.BallId)
                    continue;

                var e = _world!.GetEntity(id);
                if (e.Has<PositionComponent>())
                    e.Get<PositionComponent>().Position = new Vector2(-10000, -10000);
            }
        }

        _scrimmageOffenseIds.Clear();
        _scrimmageDefenseIds.Clear();

        var formationData = GameContent.FormationData;
        var formationSpawner = new Spawning.FormationSpawner();

        var formationId = string.IsNullOrWhiteSpace(offenseFormationId)
            ? (formationData.OffensiveFormations.Any(f => f.Id == "01") ? "01" : formationData.OffensiveFormations.First().Id)
            : offenseFormationId;

        var offenseTeam = _matchState!.PossessionTeam;
        var defenseTeam = offenseTeam == 0 ? 1 : 0;

        var offense = formationSpawner.Spawn(
            _world!,
            formationData,
            formationId: formationId,
            teamIndex: offenseTeam,
            isOffense: true,
            playerControlled: true);

        _scrimmageOffenseIds.AddRange(offense.Players.Select(p => p.EntityId));
        _scrimmageDefenseIds.AddRange(SpawnPlaceholderDefense(_world!, defenseTeam));

        _scrimmageRosterInitialized = true;
        Console.WriteLine($"[scrimmage] roster initialized formation={formationId} offenseTeam={offenseTeam} defenseTeam={defenseTeam}");
    }

    private static List<int> SpawnPlaceholderDefense(World world, int teamIndex)
    {
        var ids = new List<int>(capacity: 11);

        // DL (4)
        ids.Add(SpawnDef(world, new Vector2(0, 72), teamIndex, PlayerRole.DL, "DE-L"));
        ids.Add(SpawnDef(world, new Vector2(0, 96), teamIndex, PlayerRole.DL, "DT-L"));
        ids.Add(SpawnDef(world, new Vector2(0, 128), teamIndex, PlayerRole.DL, "DT-R"));
        ids.Add(SpawnDef(world, new Vector2(0, 152), teamIndex, PlayerRole.DL, "DE-R"));

        // LB (3)
        ids.Add(SpawnDef(world, new Vector2(-10, 84), teamIndex, PlayerRole.LB, "LB-L"));
        ids.Add(SpawnDef(world, new Vector2(-10, 112), teamIndex, PlayerRole.LB, "MLB"));
        ids.Add(SpawnDef(world, new Vector2(-10, 140), teamIndex, PlayerRole.LB, "LB-R"));

        // DB (4)
        ids.Add(SpawnDef(world, new Vector2(-22, 64), teamIndex, PlayerRole.DB, "CB-L"));
        ids.Add(SpawnDef(world, new Vector2(-22, 160), teamIndex, PlayerRole.DB, "CB-R"));
        ids.Add(SpawnDef(world, new Vector2(-30, 96), teamIndex, PlayerRole.DB, "S-L"));
        ids.Add(SpawnDef(world, new Vector2(-30, 128), teamIndex, PlayerRole.DB, "S-R"));

        return ids;
    }

    private static int SpawnDef(World world, Vector2 pos, int teamIndex, PlayerRole role, string slot)
    {
        var id = Factories.PlayerEntityFactory.CreatePlayer(world, pos, teamIndex, isPlayerControlled: false, isOffense: false, spriteId: "player_defense");
        world.GetEntity(id).Attach(new PlayerRoleComponent(role, slot));
        return id;
    }

    private void OnFlowStateChanged(GameFlowState prev, GameFlowState next)
    {
        Console.WriteLine($"[flow] {prev} -> {next}");

        if (_menuNav is not null)
            _menuNav.Enabled = next == GameFlowState.MainMenu;

        if (next == GameFlowState.TeamSelect && _matchState is not null && _flow is not null)
        {
            _flow.ConfirmTeamSelection(_matchState);
            _ignoreStartSeconds = 0.25f;
        }

        // When kickoff begins after coin toss, apply the toss result to MatchState and notify the kickoff slice.
        // This mirrors Tecmo: kickoff teams are determined by the toss winner's receive/kick choice.
        if (next == GameFlowState.CoinToss)
        {
            _ignoreStartSeconds = 0.25f;
        }

        if (next == GameFlowState.Kickoff && _matchState is not null && _flow is not null)
        {
            _flow.ApplyCoinTossToMatch(_matchState);
            _ignoreStartSeconds = 0.25f;

            // New kickoff implies new possession/teams; reset dev scrimmage bootstrap.
            _scrimmageRosterInitialized = false;
            _lastScrimmagePlaySpawnedId = -1;

            if (_events is not null)
            {
                _events.Publish(new KickoffSetupEvent(
                    KickingTeam: _flow.KickingTeamIndex,
                    ReceivingTeam: _flow.ReceivingTeamIndex,
                    Reason: KickoffSetupReason.AfterTouchdown));
            }
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

        // TODO(camera): add a simple follow camera (ball/controlled entity) by composing a translation
        // matrix before ScaleMatrix. For now we keep the fixed virtual NES viewport.

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
            if (_fieldRenderer is not null && _renderResources is not null)
                _fieldRenderer.Draw(_spriteBatch, _renderResources.Pixel);
            _world?.Draw(gameTime);

            // HUD (scoreboard, down/distance)
            if (_matchState is not null)
            {
                _scoreboardRenderer?.Draw(_spriteBatch, _matchState);
                _downDistanceRenderer?.Draw(_spriteBatch, _matchState);
            }

            // Debug info
            DrawDebugInfo(_spriteBatch);

            // Playcall placeholder overlay
            if (_playCallState is not null && _playCallState.Visible)
            {
                var font = FontSystem.Instance.GetFont(FontSize.Large);
                var pixel = _renderResources?.Pixel;
                if (pixel is not null)
                {
                    // Dim background
                    _spriteBatch.Draw(pixel, new Rectangle(0, 0, 256, 224), Color.Black * 0.75f);
                }

                if (font is not null)
                {
                    const string text = "PLAYCALL";
                    var size = font.MeasureString(text);
                    var pos = new Vector2((256 - size.X) / 2f, (224 - size.Y) / 2f);
                    _spriteBatch.DrawString(font, text, pos, Color.White);
                }
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
