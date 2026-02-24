using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame;

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    
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
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));

        // Content pipeline loaded - ready for entity system and rendering
        base.Draw(gameTime);
    }
}
