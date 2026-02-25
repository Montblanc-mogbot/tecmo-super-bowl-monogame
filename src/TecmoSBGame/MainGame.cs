using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Entities;
using TecmoSBGame.Systems;
using TecmoSBGame.Rendering;

namespace TecmoSBGame;

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private World? _world;
    private RenderViewport? _viewport;
    private FieldRenderer? _fieldRenderer;
    private GameStateSystem? _gameStateSystem;
    
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

        // Create game state system
        _gameStateSystem = new GameStateSystem();
        
        // Initialize ECS world with all systems
        _world = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(new InputSystem())
            .AddSystem(_gameStateSystem)
            .AddSystem(new RenderingSystem(_spriteBatch!, GraphicsDevice))
            .Build();
        
        Components.Add(_world);

        // Spawn the kickoff scenario
        _gameStateSystem.SpawnKickoffScenario(_world);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _fieldRenderer?.LoadContent(Content);
    }

    protected override void Update(GameTime gameTime)
    {
        // ECS systems update automatically via _world
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        
        // Begin rendering with viewport scale
        _spriteBatch!.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            effect: null,
            transformMatrix: _viewport?.ScaleMatrix);
        
        // Draw field
        _fieldRenderer?.Draw(_spriteBatch);
        
        // ECS systems draw automatically via _world
        _spriteBatch.End();
        
        base.Draw(gameTime);
    }
}
