using System.Collections.Generic;

namespace TecmoSBGame.Content.Sprites;

public sealed class SpriteManifestConfig
{
    public List<SpriteAtlasConfig> Atlases { get; set; } = new();
}

public sealed class SpriteAtlasConfig
{
    public string Id { get; set; } = "";

    /// <summary>
    /// MonoGame Content asset name (no extension), e.g. "Textures/DebugSprites".
    /// </summary>
    public string Texture { get; set; } = "";

    public Dictionary<string, SpriteRegionConfig> Sprites { get; set; } = new();
}

public sealed class SpriteRegionConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}
