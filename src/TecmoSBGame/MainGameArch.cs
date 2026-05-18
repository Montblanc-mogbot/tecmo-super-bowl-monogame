using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TecmoSBGame.Diagnostics;
using TecmoSBGame.Rendering.Hud;
using TecmoSBGame.Rendering;
using TecmoSBGame.Rendering.PostPlay;
using TecmoSBGame.Rendering.Sprites;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.Audio;
using TecmoSBGame.Persistence;
using TecmoSBGame.RuntimeCapture;
using TecmoSBGame.SimArch;
using TecmoSBGame.SimArch.Flow;
using SimMatchState = TecmoSBGame.SimArch.State.MatchState;
using SimPlayPhase = TecmoSBGame.SimArch.State.PlayPhase;
using SimPlayState = TecmoSBGame.SimArch.State.PlayState;
using SimWhistleReason = TecmoSBGame.SimArch.State.WhistleReason;

namespace TecmoSBGame;

/// <summary>
/// Arch-only game host. MonoGame.Extended.Entities (MGE) has been retired.
///
/// This class intentionally owns only:
/// - content load (YAML + sprite manifest)
/// - an Arch sim instance
/// - a renderer that draws the SimSnapshot
/// </summary>
public sealed class MainGameArch : Game
{
    // Ported from: src/TecmoSBGame/ArchiveMge/MainGame.cs

    private readonly GraphicsDeviceManager _graphics;

    private SpriteBatch? _spriteBatch;
    private RenderResources? _renderResources;
    private RenderViewport? _viewport;
    private FieldRenderer? _fieldRenderer;

    private SpriteRegistry? _spriteRegistry;
    private SimRenderer? _simRenderer;
    private PlayCallOverlayRenderer? _playCallOverlayRenderer;
    private TitleScreenRenderer? _titleRenderer;
    private MainMenuRenderer? _mainMenuRenderer;
    private TeamSelectRenderer? _teamSelectRenderer;
    private CoinTossRenderer? _coinTossRenderer;
    private SeasonMetaRenderer? _seasonMetaRenderer;
    private ScoreboardRenderer? _scoreboardRenderer;
    private DownDistanceRenderer? _downDistanceRenderer;
    private PostPlaySummaryRenderer? _postPlayRenderer;
    private HudRenderer? _hudRenderer;

    private SoundService? _sound;
    private SimArchAudioBridge? _audioBridge;
    private Sim? _sim;
    private GameFlowController? _flow;
    private SaveManager? _saveManager;
    private SeasonManager? _seasonManager;
    private SeasonPresentationService? _seasonPresentation;

    // Fixed-step sim clock
    private const float Hz = 60f;
    private const float Dt = 1f / Hz;
    private float _accumulatorSeconds;
    private float _totalTimeSeconds;

    // UI input edge tracking
    private bool _prevEnter;
    private bool _prevSpace;
    private bool _prevB;
    private bool _prevEscape;
    private bool _prevP;
    private bool _prevUp;
    private bool _prevDown;
    private bool _prevLeft;
    private bool _prevRight;

    private readonly int _runtimeCaptureTicks;
    private bool _runtimeCaptureComplete;

    public GameContent GameContent { get; private set; } = null!;

    public MainGameArch(int runtimeCaptureTicks = 0)
    {
        _runtimeCaptureTicks = runtimeCaptureTicks;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        Window.Title = "TecmoSBGame (Arch)";

        // NES aspect ratio: 256x224 = 8:7, scaled up.
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 1120; // 224 * 5
        _graphics.SynchronizeWithVerticalRetrace = true;
    }

    protected override void Initialize()
    {
        GameLog.InstallConsoleTee();
        CrashLogging.Install();

        GameContent = new GameContent(Services);
        GameContent.LoadAll();

        // NOTE: Some MonoGame initialization paths can invoke LoadContent very early.
        // Ensure render dependencies are created before base.Initialize().
        _viewport = new RenderViewport(GraphicsDevice);
        _renderResources = new RenderResources(GraphicsDevice);
        _fieldRenderer = new FieldRenderer(GraphicsDevice);

        _sound = new SoundService(Content);
        _sound.LoadDefaultCues();
        _audioBridge = new SimArchAudioBridge(_sound);

        _seasonPresentation = new SeasonPresentationService();
        _saveManager = new SaveManager();
        _seasonManager = new SeasonManager(_saveManager, _seasonPresentation);

        _sim = new Sim(
            formationData: GameContent.FormationData,
            defensiveFormationData: GameContent.DefensiveFormationData,
            playList: GameContent.PlayList,
            playData: GameContent.PlayData,
            defensePlays: GameContent.DefensePlays);

        _flow = new GameFlowController(seed: 0x5157)
        {
            TeamCount = Math.Max(1, GameContent.TeamData.Teams.Count)
        };
        _flow.Reset();
        _flow.SetActiveSeason(LoadOrCreateSeason());
        _flow.StateChanged += OnFlowStateChanged;

        // Sprite registry (optional; game can still render via debug primitives).
        var reg = new SpriteRegistry();
        if (GameContent.SpriteManifest is not null)
            reg.LoadFromManifest(Content, GameContent.SpriteManifest);
        _spriteRegistry = reg;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);
        FontSystem.Instance.Load(Content);

        if (_spriteBatch is null || _renderResources is null || _spriteRegistry is null)
            throw new InvalidOperationException("MainGameArch missing render dependencies");

        _simRenderer = new SimRenderer(_spriteBatch, _renderResources.Pixel, _spriteRegistry);
        _playCallOverlayRenderer = new PlayCallOverlayRenderer();
        _titleRenderer = new TitleScreenRenderer(GraphicsDevice);
        _mainMenuRenderer = new MainMenuRenderer(GraphicsDevice);
        _teamSelectRenderer = new TeamSelectRenderer(GraphicsDevice, GameContent);
        _coinTossRenderer = new CoinTossRenderer(GraphicsDevice);
        _seasonMetaRenderer = new SeasonMetaRenderer(GraphicsDevice);
        _scoreboardRenderer = new ScoreboardRenderer(GraphicsDevice);
        _downDistanceRenderer = new DownDistanceRenderer(GraphicsDevice);
        _postPlayRenderer = new PostPlaySummaryRenderer(GraphicsDevice);
        _hudRenderer = new HudRenderer(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_sim is null || _flow is null)
        {
            base.Update(gameTime);
            return;
        }

        _totalTimeSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape) && !_prevEscape)
            Exit();
        _prevEscape = kb.IsKeyDown(Keys.Escape);

        var enter = kb.IsKeyDown(Keys.Enter);
        var space = kb.IsKeyDown(Keys.Space);
        var bkey = kb.IsKeyDown(Keys.B);
        var pkey = kb.IsKeyDown(Keys.P);
        var up = kb.IsKeyDown(Keys.Up);
        var down = kb.IsKeyDown(Keys.Down);
        var left = kb.IsKeyDown(Keys.Left);
        var right = kb.IsKeyDown(Keys.Right);

        if (pkey && !_prevP)
            _sim.SetPaused(!_sim.Paused);

        var startPressed = enter && !_prevEnter;
        var upPressed = up && !_prevUp;
        var downPressed = down && !_prevDown;
        var leftPressed = left && !_prevLeft;
        var rightPressed = right && !_prevRight;

        if (_flow.State is GameFlowState.MainMenu or GameFlowState.TeamSelect or GameFlowState.CoinToss or GameFlowState.SeasonMeta)
        {
            if (upPressed || downPressed || leftPressed || rightPressed)
                _audioBridge?.OnMenuMove();
            if (startPressed)
                _audioBridge?.OnMenuSelect();
        }

        _flow.UpdateUiInput(startPressed, leftPressed, rightPressed, upPressed, downPressed);

        var ui = new SimArch.Components.UiButtons();
        var dir = Vector2.Zero;

        if (_flow.State is GameFlowState.Kickoff or GameFlowState.OnField or GameFlowState.PostPlay)
        {
            if (left) dir.X -= 1f;
            if (right) dir.X += 1f;
            if (up) dir.Y -= 1f;
            if (down) dir.Y += 1f;
            if (dir.LengthSquared() > 0f)
                dir.Normalize();

            ui = new SimArch.Components.UiButtons
            {
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Select = startPressed,
                Back = bkey && !_prevB,
                Snap = space && !_prevSpace,
                Continue = startPressed,
            };
        }

        _sim.SetInput(dir);
        _sim.SetUiButtons(ui);

        _prevEnter = enter;
        _prevSpace = space;
        _prevB = bkey;
        _prevP = pkey;
        _prevUp = up;
        _prevDown = down;
        _prevLeft = left;
        _prevRight = right;

        // Fixed-step update.
        if (_runtimeCaptureTicks > 0)
        {
            while (_sim.Snapshot.Tick < _runtimeCaptureTicks)
            {
                _sim.Update(Dt);
                _audioBridge?.Update(_sim.PlayState);
                if (!_sim.Paused)
                    SyncFlowWithSim();
            }
        }
        else
        {
            _accumulatorSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
            while (_accumulatorSeconds >= Dt)
            {
                _sim.Update(Dt);
                _audioBridge?.Update(_sim.PlayState);
                if (!_sim.Paused)
                    SyncFlowWithSim();
                _accumulatorSeconds -= Dt;
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null || _viewport is null || _simRenderer is null || _sim is null || _flow is null)
        {
            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _viewport.ScaleMatrix);

        switch (_flow.State)
        {
            case GameFlowState.Title:
                _titleRenderer?.Draw(_spriteBatch, (float)gameTime.TotalGameTime.TotalSeconds);
                break;

            case GameFlowState.MainMenu:
                _mainMenuRenderer?.Draw(_spriteBatch, MapMainMenuSelection(_flow.SelectedMainMenuItem));
                break;

            case GameFlowState.TeamSelect:
                _teamSelectRenderer?.Draw(_spriteBatch, _flow.AwayTeamIndex, _flow.HomeTeamIndex, _flow.ActiveTeamSelectColumn);
                break;

            case GameFlowState.CoinToss:
                if (_coinTossRenderer is not null)
                {
                    _coinTossRenderer.WindDirection = _flow.WindDirection;
                    _coinTossRenderer.Draw(_spriteBatch, _flow.TossWinnerTeamIndex, _flow.WinnerChoosesReceive, _totalTimeSeconds);
                }
                break;

            case GameFlowState.SeasonMeta:
                DrawSeasonMeta();
                break;

            case GameFlowState.Kickoff:
            case GameFlowState.OnField:
            case GameFlowState.PostPlay:
                DrawFieldState();
                if (_flow.State == GameFlowState.PostPlay)
                    DrawPostPlayOverlay();
                break;
        }

        _spriteBatch.End();

        if (_runtimeCaptureTicks > 0 && !_runtimeCaptureComplete)
        {
            SaveRuntimeCapture();
            _runtimeCaptureComplete = true;
            Exit();
        }

        base.Draw(gameTime);
    }

    private void DrawFieldState()
    {
        if (_spriteBatch is null || _sim is null || _renderResources is null || _spriteRegistry is null)
            return;

        _fieldRenderer?.Draw(_spriteBatch, _renderResources.Pixel, _spriteRegistry);
        _simRenderer?.Draw(_sim.Snapshot);

        if (_sim.PlayState.Phase == SimPlayPhase.PreSnap && !_sim.Paused)
            _playCallOverlayRenderer?.Draw(_spriteBatch, _renderResources.Pixel, _sim.Snapshot);

        var scoreboard = ToLegacyMatchState(_sim.MatchState);
        _scoreboardRenderer?.Draw(_spriteBatch, scoreboard);
        _downDistanceRenderer?.Draw(_spriteBatch, scoreboard);
        _hudRenderer?.Draw(_spriteBatch, _sim.Snapshot);

        var missing = _sound?.DescribeMissingCues();
        if (!string.IsNullOrWhiteSpace(missing) && !string.Equals(missing, "none", StringComparison.Ordinal))
        {
            var font = FontSystem.Instance.GetFont(FontSize.Small);
            if (font is not null)
                _spriteBatch.DrawString(font, $"AUDIO MISSING: {missing}", new Vector2(8, 210), Color.OrangeRed);
        }
    }

    private void DrawPostPlayOverlay()
    {
        if (_spriteBatch is null || _sim is null || _postPlayRenderer is null)
            return;

        var legacyPlay = ToLegacyPlayState(_sim.PlayState);
        var legacyMatch = ToLegacyMatchState(_sim.MatchState);
        _postPlayRenderer.Draw(_spriteBatch, legacyPlay, legacyMatch, _totalTimeSeconds);
    }

    private void DrawSeasonMeta()
    {
        if (_spriteBatch is null || _flow is null || _seasonMetaRenderer is null)
            return;

        var season = _flow.ActiveSeason ?? LoadOrCreateSeason();
        _flow.SetActiveSeason(season);
        if (_seasonPresentation is null)
            return;

        _seasonPresentation.RefreshDerivedState(season);
        var body = _flow.ActiveSeasonMetaPage switch
        {
            SeasonMetaPage.Hub => _seasonPresentation.RenderHub(season),
            SeasonMetaPage.Standings => _seasonPresentation.RenderStandings(season),
            SeasonMetaPage.Leaders => _seasonPresentation.RenderLeaders(season),
            SeasonMetaPage.Records => _seasonPresentation.RenderRecords(season),
            SeasonMetaPage.Playoffs => _seasonPresentation.RenderPlayoffs(season),
            SeasonMetaPage.ProBowl => _seasonPresentation.RenderProBowl(season),
            _ => _seasonPresentation.RenderHub(season),
        };

        var footer = "LEFT/RIGHT PAGE  ENTER NEXT  DOWN BACK";
        _seasonMetaRenderer.Draw(_spriteBatch, _flow.ActiveSeasonMetaPage.ToString().ToUpperInvariant(), body, footer);
    }

    private void SyncFlowWithSim()
    {
        if (_sim is null || _flow is null)
            return;

        if (_sim.PlayState.Phase == SimPlayPhase.PostPlay && _flow.State == GameFlowState.OnField)
            _flow.NotifyPlayEnded(_sim.PlayState.PlayId);
        else if (_sim.PlayState.Phase == SimPlayPhase.PreSnap && _flow.State == GameFlowState.PostPlay)
            _flow.NotifyNextPlayReady();
    }

    private SeasonState LoadOrCreateSeason()
    {
        if (_seasonManager is null)
            throw new InvalidOperationException("Season manager not initialized.");

        var loaded = _seasonManager.LoadSeasonState("season-ui");
        if (loaded is not null)
            return loaded;

        var teamCount = Math.Min(4, Math.Max(2, GameContent.TeamData.Teams.Count));
        if (teamCount % 2 != 0)
            teamCount--;
        var season = _seasonManager.CreateSeason("season-ui", Enumerable.Range(0, teamCount).ToArray());
        _seasonManager.SaveSeasonState(season);
        return season;
    }

    private void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
    {
        if (_sim is null || _flow is null)
            return;

        if (next == GameFlowState.TeamSelect)
        {
            _sim.Reset();
            _sim.MatchState.AwayTeamId = _flow.AwayTeamIndex;
            _sim.MatchState.HomeTeamId = _flow.HomeTeamIndex;
            return;
        }

        if (next == GameFlowState.CoinToss)
        {
            _flow.ConfirmTeamSelection(_sim.MatchState);
            return;
        }

        if (next == GameFlowState.Kickoff)
        {
            _flow.ApplyCoinTossToMatch(_sim.MatchState);
            _sim.PlayState.ResetForNewPlay(
                _sim.MatchState.PlayNumber + 1,
                SimPlayState.ToAbsoluteYard(_sim.MatchState.BallSpot, _sim.MatchState.OffenseDirection));
        }
    }

    private static int MapMainMenuSelection(SimArch.Components.MenuItemType selected)
        => selected switch
        {
            SimArch.Components.MenuItemType.Preseason => 0,
            SimArch.Components.MenuItemType.Season => 1,
            SimArch.Components.MenuItemType.ProBowl => 2,
            SimArch.Components.MenuItemType.Options => 3,
            SimArch.Components.MenuItemType.Data => 4,
            _ => 0,
        };

    private static State.MatchState ToLegacyMatchState(SimMatchState match)
    {
        var legacy = new State.MatchState
        {
            Quarter = Math.Clamp(match.Quarter, 1, 4),
            GameClockSeconds = match.GameClockSeconds,
            PossessionTeam = match.PossessionTeam,
            AwayTeamId = match.AwayTeamId,
            HomeTeamId = match.HomeTeamId,
            OffenseDirection = match.OffenseDirection == SimArch.State.OffenseDirection.LeftToRight
                ? State.OffenseDirection.LeftToRight
                : State.OffenseDirection.RightToLeft,
            Down = match.Down,
            YardsToGo = match.GoalToGo ? 0 : match.YardsToGo,
            BallSpot = match.BallSpot.OnOwnSide
                ? State.BallSpot.Own(match.BallSpot.Yards)
                : State.BallSpot.Opp(match.BallSpot.Yards),
            Team0Score = match.Team0Score,
            Team1Score = match.Team1Score,
            PlayNumber = match.PlayNumber,
            DriveId = match.DriveId,
            MatchOver = match.MatchOver,
        };

        return legacy;
    }

    private static State.PlayState ToLegacyPlayState(SimPlayState play)
    {
        var legacy = new State.PlayState
        {
            PlayId = play.PlayId,
            Phase = play.Phase == SimPlayPhase.PreSnap
                ? State.PlayPhase.PreSnap
                : play.Phase == SimPlayPhase.InPlay
                    ? State.PlayPhase.InPlay
                    : State.PlayPhase.PostPlay,
            StartAbsoluteYard = play.StartAbsoluteYard,
            EndAbsoluteYard = play.EndAbsoluteYard,
            PlayElapsedSeconds = play.PlayElapsedSeconds,
            WhistleReason = play.WhistleReason switch
            {
                SimWhistleReason.Tackle => State.WhistleReason.Tackle,
                SimWhistleReason.OutOfBounds => State.WhistleReason.OutOfBounds,
                SimWhistleReason.Touchdown => State.WhistleReason.Touchdown,
                SimWhistleReason.Safety => State.WhistleReason.Safety,
                SimWhistleReason.Incomplete => State.WhistleReason.Incomplete,
                SimWhistleReason.Touchback => State.WhistleReason.Touchback,
                SimWhistleReason.Turnover => State.WhistleReason.Turnover,
                _ => State.WhistleReason.None,
            },
            Result = new State.PlayResult(
                play.Result.YardsGained,
                play.Result.Turnover,
                play.Result.Touchdown,
                play.Result.Safety),
            AutoPlaycallEnabled = false,
        };

        return legacy;
    }

    private void SaveRuntimeCapture()
    {
        if (_sim is null)
            throw new InvalidOperationException("Sim not initialized for runtime capture.");

        var timestamp = DateTime.UtcNow;
        var captureRoot = Path.Combine("artifacts", "runtime-captures", $"capture_{timestamp:yyyyMMdd_HHmmssfff}");
        Directory.CreateDirectory(captureRoot);

        var pngPath = Path.Combine(captureRoot, "frame.png");
        using (var stream = File.Create(pngPath))
        {
            var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            using var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            var data = new Color[width * height];
            GraphicsDevice.GetBackBufferData(data);
            texture.SetData(data);
            texture.SaveAsPng(stream, width, height);
        }

        var manifest = new CaptureManifest
        {
            TimestampUtc = timestamp.ToString("O"),
            CapturedTick = _sim.Snapshot.Tick,
            RequestedTicks = _runtimeCaptureTicks,
            Quarter = _sim.Snapshot.Hud.Quarter,
            GameClockSeconds = _sim.Snapshot.Hud.GameClockSeconds,
            PossessionTeam = _sim.Snapshot.Hud.PossessionTeam,
            PlayNumber = _sim.Snapshot.Hud.PlayNumber,
            PlayPhase = _sim.PlayState.Phase.ToString(),
            StatusLine = _sim.Snapshot.Hud.StatusLine,
            SituationLabel = _sim.Snapshot.Hud.SituationLabel,
            LastPlaySummary = _sim.Snapshot.Hud.LastPlaySummary,
            ScreenshotPath = pngPath,
        };

        var manifestPath = Path.Combine(captureRoot, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[runtime-capture] PASS tick={manifest.CapturedTick} artifactDir={captureRoot} screenshot={pngPath} manifest={manifestPath}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_flow is not null)
                _flow.StateChanged -= OnFlowStateChanged;
            _sim?.Dispose();
        }
        base.Dispose(disposing);
    }
}
