using System;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering.UI;

public enum FontSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>
/// Central font access point for UI rendering.
///
/// NOTE: Fonts are content assets; if a font is missing from the Content pipeline,
/// this system will fall back to any successfully loaded font (or null).
/// Call <see cref="Load"/> once from MainGame.LoadContent().
/// </summary>
public sealed class FontSystem
{
    public static FontSystem Instance { get; } = new();

    private SpriteFont? _small;
    private SpriteFont? _medium;
    private SpriteFont? _large;

    private FontSystem() { }

    public void Load(ContentManager content)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        // These assets are expected to exist in Content/Fonts and be included in Content.mgcb.
        _small = TryLoad(content, "Fonts/UiSmall");
        _medium = TryLoad(content, "Fonts/UiMedium");
        _large = TryLoad(content, "Fonts/UiLarge");
    }

    public SpriteFont? GetFont(FontSize size)
    {
        return size switch
        {
            FontSize.Small => _small ?? _medium ?? _large,
            FontSize.Medium => _medium ?? _small ?? _large,
            FontSize.Large => _large ?? _medium ?? _small,
            _ => _medium ?? _small ?? _large,
        };
    }

    private static SpriteFont? TryLoad(ContentManager content, string assetName)
    {
        try
        {
            return content.Load<SpriteFont>(assetName);
        }
        catch
        {
            return null;
        }
    }
}
