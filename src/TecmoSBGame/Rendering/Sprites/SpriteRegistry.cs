using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Content.Sprites;

namespace TecmoSBGame.Rendering.Sprites;

public sealed class SpriteRegistry
{
    private readonly Dictionary<string, (Texture2D tex, Rectangle src)> _regions = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string spriteId, out Texture2D texture, out Rectangle source)
    {
        if (_regions.TryGetValue(spriteId, out var v))
        {
            texture = v.tex;
            source = v.src;
            return true;
        }

        texture = null!;
        source = default;
        return false;
    }

    public void LoadFromManifest(ContentManager content, SpriteManifestConfig manifest)
    {
        if (manifest?.Atlases is null)
            return;

        foreach (var atlas in manifest.Atlases)
        {
            if (string.IsNullOrWhiteSpace(atlas.Texture))
                continue;

            Texture2D tex;
            try
            {
                tex = content.Load<Texture2D>(atlas.Texture);
            }
            catch
            {
                // Skip missing textures (keep registry partial).
                continue;
            }

            if (atlas.Sprites is null)
                continue;

            foreach (var (id, r) in atlas.Sprites)
            {
                if (string.IsNullOrWhiteSpace(id) || r is null)
                    continue;

                _regions[id] = (tex, new Rectangle(r.X, r.Y, r.W, r.H));
            }
        }
    }
}
