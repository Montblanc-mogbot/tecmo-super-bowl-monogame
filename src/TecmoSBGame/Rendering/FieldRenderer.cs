using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders the football field.
/// </summary>
public sealed class FieldRenderer
{
    private Texture2D? _fieldTexture;
    private Texture2D? _yardLineTexture;
    private SpriteFont? _font;

    // Field dimensions live in TecmoSBGame.Field.FieldBounds (single source of truth).

    public FieldRenderer(GraphicsDevice graphicsDevice)
    {
        // NOTE: FieldRenderer should not allocate textures per-frame.
        // Any shared GPU resources (e.g., a 1x1 pixel) should be provided at draw time.
    }

    public void LoadContent(ContentManager content)
    {
        // TODO: Load actual field texture
        // _fieldTexture = content.Load<Texture2D>("field/grass");
        // _yardLineTexture = content.Load<Texture2D>("field/yardline");
        // _font = content.Load<SpriteFont>("fonts/yardnumbers");
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (spriteBatch is null) throw new ArgumentNullException(nameof(spriteBatch));
        if (pixel is null) throw new ArgumentNullException(nameof(pixel));

        // Draw field background
        DrawFieldBackground(spriteBatch, pixel);

        // Draw yard lines
        DrawYardLines(spriteBatch, pixel);

        // Draw yard numbers
        DrawYardNumbers(spriteBatch);

        // Draw end zones
        DrawEndZones(spriteBatch, pixel);
    }
    
    private void DrawFieldBackground(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Solid green field
        var fieldRect = new Rectangle(
            Field.FieldBounds.FieldLeftX,
            Field.FieldBounds.FieldTopY,
            Field.FieldBounds.FieldRightX - Field.FieldBounds.FieldLeftX,
            Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);

        var grassColor = new Color(0, 120, 0); // Tecmo green
        spriteBatch.Draw(pixel, fieldRect, grassColor);
    }

    private void DrawYardLines(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw yard lines every 10 yards
        for (int yard = 0; yard <= 100; yard += 10)
        {
            int x = YardToX(yard);
            var lineRect = new Rectangle(x, Field.FieldBounds.FieldTopY, 1,
                Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
            spriteBatch.Draw(pixel, lineRect, Color.White);
        }

        // Draw thicker goal lines
        int goalLine0 = YardToX(0);
        int goalLine100 = YardToX(100);

        spriteBatch.Draw(pixel,
            new Rectangle(goalLine0 - 1, Field.FieldBounds.FieldTopY, 2,
                Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY),
            Color.White);
        spriteBatch.Draw(pixel,
            new Rectangle(goalLine100 - 1, Field.FieldBounds.FieldTopY, 2,
                Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY),
            Color.White);
    }
    
    private void DrawYardNumbers(SpriteBatch spriteBatch)
    {
        if (_font == null) return;
        
        // Draw yard numbers at 10, 20, 30, 40, 50, 40, 30, 20, 10
        for (int yard = 10; yard < 50; yard += 10)
        {
            int x1 = YardToX(yard);
            int x2 = YardToX(100 - yard);
            
            string text = yard.ToString();
            var size = _font.MeasureString(text);
            
            // Top numbers
            spriteBatch.DrawString(_font, text, new Vector2(x1 - size.X / 2, Field.FieldBounds.FieldTopY + 5), Color.White);
            spriteBatch.DrawString(_font, text, new Vector2(x2 - size.X / 2, Field.FieldBounds.FieldTopY + 5), Color.White);
            
            // Bottom numbers (flipped)
            spriteBatch.DrawString(_font, text, new Vector2(x1 - size.X / 2, Field.FieldBounds.FieldBottomY - 15), Color.White);
            spriteBatch.DrawString(_font, text, new Vector2(x2 - size.X / 2, Field.FieldBounds.FieldBottomY - 15), Color.White);
        }
        
        // 50 yard line
        int x50 = YardToX(50);
        var size50 = _font.MeasureString("50");
        spriteBatch.DrawString(_font, "50", new Vector2(x50 - size50.X / 2, Field.FieldBounds.FieldTopY + 5), Color.White);
        spriteBatch.DrawString(_font, "50", new Vector2(x50 - size50.X / 2, Field.FieldBounds.FieldBottomY - 15), Color.White);
    }
    
    private void DrawEndZones(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Solid color end zones
        var endZoneColor = new Color(0, 80, 0);

        int goalLine100 = YardToX(100);

        // Left end zone
        var leftEndZone = new Rectangle(
            Field.FieldBounds.FieldLeftX - Field.FieldBounds.EndZoneDepth,
            Field.FieldBounds.FieldTopY,
            Field.FieldBounds.EndZoneDepth,
            Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
        spriteBatch.Draw(pixel, leftEndZone, endZoneColor);

        // Right end zone
        var rightEndZone = new Rectangle(
            goalLine100,
            Field.FieldBounds.FieldTopY,
            Field.FieldBounds.EndZoneDepth,
            Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
        spriteBatch.Draw(pixel, rightEndZone, endZoneColor);
    }

    private int YardToX(int yard)
    {
        // Map 0-100 yards to field coordinates
        float yardWidth = (Field.FieldBounds.FieldRightX - Field.FieldBounds.FieldLeftX) / 100f;
        return Field.FieldBounds.FieldLeftX + (int)(yard * yardWidth);
    }
}
