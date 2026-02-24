using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Entities;
using TecmoSBGame.Systems;

namespace TecmoSBGame;

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private World? _world;
    
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
        
        // Load all YAML content at startup
        GameContent = new GameContent(Services);
        GameContent.LoadAll();
        
        // Initialize ECS world
        _world = new WorldBuilder()
            .AddSystem(new MovementSystem())
            .AddSystem(new RenderingSystem(_spriteBatch!, GraphicsDevice))
            .Build();
        
        Components.Add(_world);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        // ECS systems update automatically via _world
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));

        // ECS systems draw automatically via _world
        base.Draw(gameTime);
    }
}
