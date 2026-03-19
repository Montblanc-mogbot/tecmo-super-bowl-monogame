using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.Sprites;
using TecmoSBGame.SimArch;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders an Arch simulation snapshot.
/// </summary>
public sealed class SimRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteRegistry _sprites;

    private readonly TecmoSBGame.Rendering.Hud.HudRenderer _hud = new();

    public SimRenderer(SpriteBatch spriteBatch, Texture2D pixel, SpriteRegistry sprites)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _sprites = sprites;
    }

    public void Draw(SimSnapshot snapshot)
    {
        // HUD (top overlay)
        _hud.Draw(_spriteBatch, snapshot);

        // Players
        foreach (var p in snapshot.Players)
        {
            var rect = new Rectangle((int)p.Position.X - 8, (int)p.Position.Y - 8, 16, 16);

            if (!string.IsNullOrWhiteSpace(p.SpriteId) && _sprites.TryGet(p.SpriteId, out var tex, out var src))
            {
                _spriteBatch.Draw(tex, rect, src, Color.White);
            }
            else
            {
                var c = p.IsOffense ? Color.CornflowerBlue : Color.OrangeRed;
                _spriteBatch.Draw(_pixel, rect, c);
            }
        }

        // Ball
        if (snapshot.Ball.SpriteId is { Length: > 0 } && _sprites.TryGet(snapshot.Ball.SpriteId, out var btex, out var bsrc))
        {
            var rect = new Rectangle((int)snapshot.Ball.Position.X - 4, (int)snapshot.Ball.Position.Y - 4, 8, 8);
            _spriteBatch.Draw(btex, rect, bsrc, Color.White);
        }
        else if (snapshot.Ball.OwnerEntityId != 0 || snapshot.Ball.IsHeld)
        {
            var rect = new Rectangle((int)snapshot.Ball.Position.X - 3, (int)snapshot.Ball.Position.Y - 3, 6, 6);
            _spriteBatch.Draw(_pixel, rect, Color.White);
        }
    }
}
