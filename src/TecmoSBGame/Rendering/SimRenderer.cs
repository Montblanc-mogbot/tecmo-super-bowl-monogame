using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TecmoSBGame.Rendering.Sprites;
using TecmoSBGame.Rendering.UI;
using TecmoSBGame.SimArch;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders an Arch simulation snapshot.
/// </summary>
public sealed class SimRenderer
{
    private static readonly Color OffenseTint = new(88, 170, 255, 170);
    private static readonly Color DefenseTint = new(255, 106, 106, 170);
    private static readonly Color ControlledOutline = new(255, 220, 120);

    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteRegistry _sprites;

    public SimRenderer(SpriteBatch spriteBatch, Texture2D pixel, SpriteRegistry sprites)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _sprites = sprites;
    }

    public void Draw(SimSnapshot snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

            // Players
        foreach (var p in snapshot.Players)
        {
            var rect = new Rectangle((int)p.Position.X - 8, (int)p.Position.Y - 8, 16, 16);

            if (!string.IsNullOrWhiteSpace(p.SpriteId) && _sprites.TryGet(p.SpriteId, out var tex, out var src))
            {
                _spriteBatch.Draw(tex, rect, src, Color.White);
                _spriteBatch.Draw(_pixel, rect, GetTeamTint(p));
            }
            else
            {
                _spriteBatch.Draw(_pixel, rect, GetTeamTint(p));
            }

            DrawBorder(rect, p.IsPlayerControlled ? ControlledOutline : Color.Black);
            if (p.HasBall)
                DrawBorder(Inflate(rect, 2), Color.White);

            DrawPlayerLabel(p, rect);
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

    private void DrawPlayerLabel(SimSnapshot.PlayerSnapshot player, Rectangle rect)
    {
        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font is null)
            return;

        var role = Abbreviate(player.Role);
        var slot = Abbreviate(player.Slot);
        var label = string.IsNullOrWhiteSpace(slot) ? role : $"{role}/{slot}";
        if (string.IsNullOrWhiteSpace(label))
            return;

        var pos = new Vector2(rect.Center.X - (font.MeasureString(label).X / 2f), rect.Y - 12);
        _spriteBatch.DrawString(font, label, pos + new Vector2(1, 1), Color.Black);
        _spriteBatch.DrawString(font, label, pos, Color.White);
    }

    private static string Abbreviate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        if (value.Length <= 4)
            return value.ToUpperInvariant();

        return value[..4].ToUpperInvariant();
    }

    private static Color GetTeamTint(SimSnapshot.PlayerSnapshot player) => player.IsOffense ? OffenseTint : DefenseTint;

    private void DrawBorder(Rectangle rect, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }

    private static Rectangle Inflate(Rectangle rect, int amount) => new(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2), rect.Height + (amount * 2));
}
