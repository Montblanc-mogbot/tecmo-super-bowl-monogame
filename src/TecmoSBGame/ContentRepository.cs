using System;
using System.Collections.Generic;
using TecmoSB;
using TecmoSB.Content;

namespace TecmoSBGame;

/// <summary>
/// Central repository for accessing all game content.
/// Provides typed access to YAML data and MonoGame assets.
/// 
/// Note: This uses the existing loader pattern for backwards compatibility.
/// New content can use the generic YamlContentLoader.
/// </summary>
public sealed class ContentRepository
{
    private readonly TecmoContentManager _content;
    private readonly string _yamlRoot;

    public ContentRepository(IServiceProvider serviceProvider, string yamlContentRoot = "content")
    {
        _content = new TecmoContentManager(serviceProvider, "Content", yamlContentRoot);
        _yamlRoot = yamlContentRoot;
    }

    // Team Data - using existing loaders
    public TeamDataConfig LoadTeamData()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "teamdata/bank1_2_team_data.yaml");
        return TeamDataYamlLoader.LoadFromFile(path);
    }

    public TeamTextDataConfig LoadTeamTextData()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "teamtext/bank16_team_text_data.yaml");
        return TeamTextDataYamlLoader.LoadFromFile(path);
    }

    // Formations
    public FormationDataConfig LoadFormationData()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "formations/formation_data.yaml");
        return FormationDataYamlLoader.LoadFromFile(path);
    }

    // Plays
    public PlayListConfig LoadPlayList()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "playcall/playlist.yaml");
        return PlayListYamlLoader.LoadFromFile(path);
    }

    public PlayDataConfig LoadPlayData()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "playdata/bank5_6_play_data.yaml");
        return PlayDataYamlLoader.LoadFromFile(path);
    }

    public DefensePlayConfig LoadDefensePlays()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "defenseplays/bank4_defense_special_pointers.yaml");
        return DefensePlayYamlLoader.LoadFromFile(path);
    }

    // Game Config
    public SimConfig LoadSimConfig()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "sim/config.yaml");
        return SimConfigYamlLoader.LoadFromFile(path);
    }

    public GameLoopConfig LoadGameLoopConfig()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "gameloop/bank17_18_main_game_loop.yaml");
        return GameLoopYamlLoader.LoadFromFile(path);
    }

    public OnFieldLoopConfig LoadOnFieldLoopConfig()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "onfieldloop/bank19_20_on_field_gameplay_loop.yaml");
        return OnFieldLoopYamlLoader.LoadFromFile(path);
    }

    // Field & Gameplay
    public FieldConfig LoadFieldConfig()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "field/bank23_field_ball_anim_collision.yaml");
        return FieldYamlLoader.LoadFromFile(path);
    }

    public FieldLayoutConfig LoadFieldLayout()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "field/field_layout.yaml");
        return FieldLayoutYamlLoader.LoadFromFile(path);
    }

    public FgWorksheetConfig LoadFgWorksheet()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "fieldgoal/fg_worksheet.yaml");
        return FgWorksheetYamlLoader.LoadFromFile(path);
    }

    // Sprite Scripts
    public Bank9SpriteScriptConfig LoadBank9SpriteScripts()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "spritescripts_bank9/bank9_sprite_scripts.yaml");
        return Bank9SpriteScriptYamlLoader.LoadFromFile(path);
    }

    // Sound
    public SoundEngineConfig LoadSoundEngine()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "sound/bank28_sound_engine.yaml");
        return SoundEngineYamlLoader.LoadFromFile(path);
    }

    public SoundDataConfig LoadSoundData()
    {
        var path = System.IO.Path.Combine(_yamlRoot, "sounddata/bank29_sound_data.yaml");
        return SoundDataYamlLoader.LoadFromFile(path);
    }

    /// <summary>
    /// Generic YAML loader for new content types.
    /// </summary>
    public YamlContentLoader YamlLoader => _content.YamlLoader;

    /// <summary>
    /// MonoGame content manager for textures, sounds, etc.
    /// </summary>
    public TecmoContentManager Manager => _content;

    /// <summary>
    /// Unloads all content.
    /// </summary>
    public void Unload() => _content.Unload();
}
