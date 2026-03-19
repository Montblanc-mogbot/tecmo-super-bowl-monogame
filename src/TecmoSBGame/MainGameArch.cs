using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TecmoSBGame.Diagnostics;
using TecmoSBGame.Rendering;
using TecmoSBGame.Rendering.Sprites;
using TecmoSBGame.SimArch;

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

    private Sim? _sim;

    // Fixed-step sim clock
    private const float Hz = 60f;
    private const float Dt = 1f / Hz;
    private float _accumulatorSeconds;

    // Temporary Arch-native flow scaffold (headless-friendly):
    // - Enter starts a play (auto-pick play 10)
    // - After tackle whistle, Enter advances to next play (pre-snap)
    private bool _playAppliedThisDown;

    public GameContent GameContent { get; private set; } = null!;

    public MainGameArch()
    {
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

        base.Initialize();

        _viewport = new RenderViewport(GraphicsDevice);
        _renderResources = new RenderResources(GraphicsDevice);
        _fieldRenderer = new FieldRenderer(GraphicsDevice);

        _sim = new Sim(GameContent.FormationData, GameContent.PlayData);

        // Sprite registry (optional; game can still render via debug primitives).
        var reg = new SpriteRegistry();
        if (GameContent.SpriteManifest is not null)
            reg.LoadFromManifest(Content, GameContent.SpriteManifest);
        _spriteRegistry = reg;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);

        if (_spriteBatch is null || _renderResources is null || _spriteRegistry is null)
            throw new InvalidOperationException("MainGameArch missing render dependencies");

        _simRenderer = new SimRenderer(_spriteBatch, _renderResources.Pixel, _spriteRegistry);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_sim is null)
        {
            base.Update(gameTime);
            return;
        }

        // Basic input: move controlled player with arrow keys.
        var dir = Vector2.Zero;
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Left)) dir.X -= 1f;
        if (kb.IsKeyDown(Keys.Right)) dir.X += 1f;
        if (kb.IsKeyDown(Keys.Up)) dir.Y -= 1f;
        if (kb.IsKeyDown(Keys.Down)) dir.Y += 1f;
        if (dir.LengthSquared() > 0f)
            dir.Normalize();

        _sim.SetInput(dir);

        // Arch-native flow scaffold:
        // - PreSnap: press Enter to apply a play (auto-pick play 10 for now)
        // - PostPlay: press Enter to advance to next play
        if (_sim.PlayState.Phase == SimArch.State.PlayPhase.PreSnap)
        {
            if (kb.IsKeyDown(Keys.Enter) && !_playAppliedThisDown)
            {
                _playAppliedThisDown = true;
                _sim.ApplyPlaySelection(new Sim.PendingPlaySelection(
                    PlayNumber: 10,
                    FormationId: "00",
                    OffensivePlayName: "AUTO",
                    OffensivePlaySlot: "AUTO"));
            }
        }
        else if (_sim.PlayState.Phase == SimArch.State.PlayPhase.PostPlay)
        {
            if (kb.IsKeyDown(Keys.Enter))
            {
                _playAppliedThisDown = false;
                _sim.AdvanceToNextPlay();
            }
        }
        else
        {
            // InPlay
            if (!kb.IsKeyDown(Keys.Enter))
                _playAppliedThisDown = false;
        }

        // Fixed-step update.
        _accumulatorSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        while (_accumulatorSeconds >= Dt)
        {
            _sim.Update(Dt);
            _accumulatorSeconds -= Dt;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_spriteBatch is null || _viewport is null || _simRenderer is null || _sim is null)
        {
            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.Clear(Color.Black);

        // Render in virtual 256x224 coordinates with a simple scale matrix.
        // (Letterboxing can be added later using DestinationRect if desired.)
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _viewport.ScaleMatrix);

        // Field background (tile fallback if sprites missing).
        _fieldRenderer?.Draw(_spriteBatch, _renderResources!.Pixel, _spriteRegistry);

        _simRenderer.Draw(_sim.Snapshot);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sim?.Dispose();
        }
        base.Dispose(disposing);
    }
}
