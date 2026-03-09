using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering.UI;

public sealed class InputPromptRenderer
{
    public void DrawPressStart(SpriteBatch spriteBatch, Vector2 position, float timeSeconds)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));

        var font = FontSystem.Instance.GetFont(FontSize.Medium);
        if (font is null)
            return;

        // Blink: ~2.5Hz with a 50% duty cycle.
        var phase = (timeSeconds * 2.5f) % 1f;
        if (phase > 0.5f)
            return;

        const string text = "PRESS START TO CONTINUE";
        spriteBatch.DrawString(font, text, position, UiColors.TextWhite);
    }

    public void DrawButtonPrompt(SpriteBatch spriteBatch, string button, string action, Vector2 position)
    {
        if (spriteBatch is null)
            throw new ArgumentNullException(nameof(spriteBatch));

        var font = FontSystem.Instance.GetFont(FontSize.Small);
        if (font is null)
            return;

        button = (button ?? string.Empty).Trim();
        action = (action ?? string.Empty).Trim();

        var text = string.IsNullOrWhiteSpace(button)
            ? action
            : string.IsNullOrWhiteSpace(action)
                ? button
                : $"{button}: {action}";

        spriteBatch.DrawString(font, text, position, UiColors.TextGray);
    }
}
