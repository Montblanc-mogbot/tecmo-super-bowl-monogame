using System;
using Microsoft.Xna.Framework;
using TecmoSB;

namespace TecmoSBGame;

/// <summary>
/// Loads and provides access to all game content at startup.
/// All YAML data is loaded once and cached for the game session.
/// </summary>
public sealed class GameContent
{
    private readonly ContentRepository _repository;
    
    // Cached content - loaded once at startup
    public TeamDataConfig TeamData { get; private set; } = null!;
    public TeamTextDataConfig TeamTextData { get; private set; } = null!;
    public FormationDataConfig FormationData { get; private set; } = null!;
    public PlayListConfig PlayList { get; private set; } = null!;
    public PlayDataConfig PlayData { get; private set; } = null!;
    public DefensePlayConfig DefensePlays { get; private set; } = null!;
    public SimConfig SimConfig { get; private set; } = null!;
    public GameLoopConfig GameLoop { get; private set; } = null!;
    public OnFieldLoopConfig OnFieldLoop { get; private set; } = null!;
    public FieldConfig FieldConfig { get; private set; } = null!;
    public FieldLayoutConfig FieldLayout { get; private set; } = null!;
    public FgWorksheetConfig FgWorksheet { get; private set; } = null!;
    public Bank9SpriteScriptConfig Bank9SpriteScripts { get; private set; } = null!;
    public SoundEngineConfig SoundEngine { get; private set; } = null!;
    public SoundDataConfig SoundData { get; private set; } = null!;

    public GameContent(IServiceProvider serviceProvider)
    {
        _repository = new ContentRepository(serviceProvider);
    }

    /// <summary>
    /// Loads all content at game startup.
    /// Call this from Game1.Initialize() or Game1.LoadContent().
    /// </summary>
    public void LoadAll()
    {
        Console.WriteLine("[GameContent] Loading all game data...");
        
        try
        {
            // Core game data
            TeamData = _repository.LoadTeamData();
            Console.WriteLine("[GameContent] Loaded team data");
            
            TeamTextData = _repository.LoadTeamTextData();
            Console.WriteLine("[GameContent] Loaded team text data");
            
            FormationData = _repository.LoadFormationData();
            Console.WriteLine("[GameContent] Loaded formation data");
            
            PlayList = _repository.LoadPlayList();
            Console.WriteLine("[GameContent] Loaded play list");
            
            PlayData = _repository.LoadPlayData();
            Console.WriteLine("[GameContent] Loaded play data");
            
            DefensePlays = _repository.LoadDefensePlays();
            Console.WriteLine("[GameContent] Loaded defense plays");
            
            // Game systems
            SimConfig = _repository.LoadSimConfig();
            Console.WriteLine("[GameContent] Loaded sim config");
            
            GameLoop = _repository.LoadGameLoopConfig();
            Console.WriteLine("[GameContent] Loaded game loop config");
            
            OnFieldLoop = _repository.LoadOnFieldLoopConfig();
            Console.WriteLine("[GameContent] Loaded on-field loop config");
            
            // Field
            FieldConfig = _repository.LoadFieldConfig();
            Console.WriteLine("[GameContent] Loaded field config");
            
            FieldLayout = _repository.LoadFieldLayout();
            Console.WriteLine("[GameContent] Loaded field layout");
            
            FgWorksheet = _repository.LoadFgWorksheet();
            Console.WriteLine("[GameContent] Loaded FG worksheet");
            
            // Sprites
            Bank9SpriteScripts = _repository.LoadBank9SpriteScripts();
            Console.WriteLine("[GameContent] Loaded sprite scripts");
            
            // Sound
            SoundEngine = _repository.LoadSoundEngine();
            Console.WriteLine("[GameContent] Loaded sound engine");
            
            SoundData = _repository.LoadSoundData();
            Console.WriteLine("[GameContent] Loaded sound data");
            
            Console.WriteLine("[GameContent] All content loaded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameContent] ERROR: Failed to load content: {ex.Message}");
            Console.WriteLine($"[GameContent] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
