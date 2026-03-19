using System;
using System.IO;
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
    public DefensiveFormationDataConfig DefensiveFormationData { get; private set; } = null!;
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

    // Sprites
    public Content.Sprites.SpriteManifestConfig? SpriteManifest { get; private set; }

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

            DefensiveFormationData = _repository.LoadDefensiveFormationData();
            Console.WriteLine("[GameContent] Loaded defensive formation data");
            
            PlayList = _repository.LoadPlayList();
            Console.WriteLine("[GameContent] Loaded play list");
            
            PlayData = _repository.LoadPlayData();
            Console.WriteLine("[GameContent] Loaded play data");

            // Cross-file YAML validation pass (references, ranges, missing ids, etc.)
            // Dev reality: we often load a partial subset of Tecmo content while iterating.
            // So by default we WARN+FILTER instead of hard-failing. Set TECMOSB_STRICT_YAML=1 to fail hard.
            var yamlIssues = ContentValidation.YamlContentValidator.Validate(FormationData, PlayList, PlayData);
            if (yamlIssues.Count > 0)
            {
                Console.WriteLine($"[GameContent] YAML VALIDATION FAILED ({yamlIssues.Count} issue(s)):");
                foreach (var issue in yamlIssues)
                    Console.WriteLine($"  - {issue}");

                var strict = string.Equals(Environment.GetEnvironmentVariable("TECMOSB_STRICT_YAML"), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("TECMOSB_STRICT_YAML"), "true", StringComparison.OrdinalIgnoreCase);

                if (strict)
                    throw new InvalidDataException($"YAML validation failed with {yamlIssues.Count} issue(s). See log for details.");

                Console.WriteLine("[GameContent] Continuing despite YAML issues (non-strict). Filtering invalid plays/formations.");
                (FormationData, PlayList) = FilterInvalidReferences(FormationData, PlayList, PlayData);
            }

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
            
            // TODO: Fix YAML format issues
            // FgWorksheet = _repository.LoadFgWorksheet();
            // Console.WriteLine("[GameContent] Loaded FG worksheet");
            
            // Sprites
            SpriteManifest = _repository.TryLoadSpriteManifest();
            if (SpriteManifest is not null)
                Console.WriteLine("[GameContent] Loaded sprite manifest");
            
            // Sound
            // SoundEngine = _repository.LoadSoundEngine();
            // Console.WriteLine("[GameContent] Loaded sound engine");
            
            // SoundData = _repository.LoadSoundData();
            // Console.WriteLine("[GameContent] Loaded sound data");
            
            Console.WriteLine("[GameContent] All content loaded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameContent] ERROR: Failed to load content: {ex.Message}");
            Console.WriteLine($"[GameContent] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private static (FormationDataConfig formationData, PlayListConfig playList) FilterInvalidReferences(
        FormationDataConfig formationData,
        PlayListConfig playList,
        PlayDataConfig playData)
    {
        var validFormationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in formationData.OffensiveFormations)
            validFormationIds.Add(f.Id);

        var validPlayNumbers = new HashSet<int>();
        foreach (var p in playData.Plays)
            validPlayNumbers.Add(p.PlayNumber);

        // Filter playlist entries to only those with known formations and at least one known play_number.
        var filteredEntries = new List<PlayEntry>();
        foreach (var e in playList.PlayList)
        {
            if (!validFormationIds.Contains(e.Formation))
                continue;

            var keepNums = e.PlayNumbers.Where(validPlayNumbers.Contains).Distinct().ToArray();
            if (keepNums.Length == 0)
                continue;

            filteredEntries.Add(new PlayEntry(
                Name: e.Name,
                Slot: e.Slot,
                Formation: e.Formation,
                PlayNumbers: keepNums,
                Defense: e.Defense));
        }

        // Filter FormationTypes to known formation ids so playcall lists don't include missing formations.
        var filteredTypes = new List<FormationType>();
        foreach (var t in formationData.FormationTypes)
        {
            var ids = t.FormationIds.Where(validFormationIds.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            filteredTypes.Add(new FormationType(t.Id, ids));
        }

        var filteredFormationData = new FormationDataConfig(
            OffensiveFormations: formationData.OffensiveFormations,
            CommandReference: formationData.CommandReference,
            FormationTypes: filteredTypes,
            Notes: formationData.Notes);

        var filteredPlayList = new PlayListConfig(
            PlayList: filteredEntries,
            Slots: playList.Slots,
            Notes: playList.Notes);

        Console.WriteLine($"[GameContent] Filtered playlist: {playList.PlayList.Count} -> {filteredEntries.Count} entries");
        return (filteredFormationData, filteredPlayList);
    }
}
