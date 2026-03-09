using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Coin toss screen renderer.
/// Virtual resolution: 256x224 (NES).
/// </summary>
public sealed class CoinTossRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly NesHudFont _font = new();
    private Texture2D? _pixel;

    public CoinTossRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    private Texture2D Pixel
    {
        get
        {
            if (_pixel is not null)
                return _pixel;

            var tex = new Texture2D(_graphicsDevice, 1, 1);
            tex.SetData(new[] { Color.White });
            _pixel = tex;
            return tex;
        }
    }

    /// <summary>
    /// Visual-only wind direction. -1 = left, +1 = right.
    /// </summary>
    public int WindDirection { get; set; } = +1;

    /// <param name="tossWinner">-1 while unresolved, else 0=away,1=home.</param>
    public void Draw(SpriteBatch sb, int tossWinner, bool winnerChoosesReceive, float animTime)
    {
        sb.Draw(Pixel, new Rectangle(0, 0, 256, 224), new Color(0, 102, 204));

        // Backplate.
        sb.Draw(Pixel, new Rectangle(24, 24, 208, 176), new Color(0, 0, 0, 180));
        sb.Draw(Pixel, new Rectangle(26, 26, 204, 172), new Color(12, 28, 80));

        // Team banners.
        DrawBanner(sb, new Rectangle(40, 36, 176, 20), label: "AWAY", highlight: tossWinner == 0);
        DrawBanner(sb, new Rectangle(40, 168, 176, 20), label: "HOME", highlight: tossWinner == 1);

        // Coin flip animation area.
        var coinRect = new Rectangle(104, 88, 48, 48);
        sb.Draw(Pixel, coinRect, new Color(0, 0, 0, 120));

        // Simple "coin" pulse.
        float pulse = 0.5f + 0.5f * MathF.Sin(animTime * 6f);
        int inset = 6 + (int)MathF.Round(pulse * 3f);
        var coinInner = new Rectangle(coinRect.X + inset, coinRect.Y + inset, coinRect.Width - inset * 2, coinRect.Height - inset * 2);
        sb.Draw(Pixel, coinInner, new Color(255, 220, 120));

        // Selection panels.
        if (tossWinner < 0)
        {
            _font.Draw(sb, Pixel, "CALL IT", new Vector2(84, 64), Color.White);
            DrawTwoChoice(sb, y: 144, left: "HEADS", right: "TAILS", leftSelected: winnerChoosesReceive);
        }
        else
        {
            _font.Draw(sb, Pixel, "WINNER", new Vector2(92, 64), Color.White);
            DrawTwoChoice(sb, y: 144, left: "RECEIVE", right: "KICK", leftSelected: winnerChoosesReceive);

            _font.Draw(sb, Pixel, "START TO CONFIRM", new Vector2(56, 156), new Color(220, 220, 220));
        }

        // Wind indicator.
        DrawWind(sb);
    }

    private void DrawBanner(SpriteBatch sb, Rectangle rect, string label, bool highlight)
    {
        var baseCol = new Color(0, 0, 0, 120);
        var hiCol = new Color(255, 220, 120);
        sb.Draw(Pixel, rect, highlight ? hiCol : baseCol);

        var textCol = highlight ? new Color(40, 40, 60) : Color.White;
        _font.Draw(sb, Pixel, label, new Vector2(rect.X + 6, rect.Y + 6), textCol);

        if (highlight)
            sb.Draw(Pixel, new Rectangle(rect.Right - 10, rect.Y + 6, 6, 8), Color.White);
    }

    private void DrawTwoChoice(SpriteBatch sb, int y, string left, string right, bool leftSelected)
    {
        var leftRect = new Rectangle(52, y, 72, 16);
        var rightRect = new Rectangle(132, y, 72, 16);

        DrawChoice(sb, leftRect, left, leftSelected);
        DrawChoice(sb, rightRect, right, !leftSelected);
    }

    private void DrawChoice(SpriteBatch sb, Rectangle rect, string label, bool selected)
    {
        var back = selected ? new Color(240, 240, 240) : new Color(0, 0, 0, 80);
        var text = selected ? new Color(40, 40, 60) : new Color(220, 220, 220);

        sb.Draw(Pixel, rect, back);
        _font.Draw(sb, Pixel, label, new Vector2(rect.X + 6, rect.Y + 5), text);

        if (selected)
            sb.Draw(Pixel, new Rectangle(rect.X - 8, rect.Y + 4, 6, 8), new Color(255, 220, 120));
    }

    private void DrawWind(SpriteBatch sb)
    {
        var baseY = 204;
        _font.Draw(sb, Pixel, "WIND", new Vector2(16, baseY), Color.White);

        bool right = WindDirection >= 0;
        string arrow = right ? ">>>" : "<<<";
        _font.Draw(sb, Pixel, arrow, new Vector2(54, baseY), new Color(255, 220, 120));

        // Simple arrow bar.
        var bar = new Rectangle(90, baseY + 2, 150, 5);
        sb.Draw(Pixel, bar, new Color(0, 0, 0, 120));
        int tipX = right ? bar.Right - 8 : bar.X + 2;
        sb.Draw(Pixel, new Rectangle(tipX, bar.Y, 6, 5), new Color(255, 220, 120));
    }
}
