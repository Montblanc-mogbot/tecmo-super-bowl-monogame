using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TecmoSBGame.Rendering;

/// <summary>
/// Renders the football field.
/// </summary>
public sealed class FieldRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D? _fieldTexture;
    private Texture2D? _yardLineTexture;
    private SpriteFont? _font;
    
    // Field dimensions live in TecmoSBGame.Field.FieldBounds (single source of truth).
    
    public FieldRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }
    
    public void LoadContent(ContentManager content)
    {
        // TODO: Load actual field texture
        // _fieldTexture = content.Load<Texture2D>("field/grass");
        // _yardLineTexture = content.Load<Texture2D>("field/yardline");
        // _font = content.Load<SpriteFont>("fonts/yardnumbers");
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        // Draw field background
        DrawFieldBackground(spriteBatch);
        
        // Draw yard lines
        DrawYardLines(spriteBatch);
        
        // Draw yard numbers
        DrawYardNumbers(spriteBatch);
        
        // Draw end zones
        DrawEndZones(spriteBatch);
    }
    
    private void DrawFieldBackground(SpriteBatch spriteBatch)
    {
        // Solid green field
        var fieldRect = new Rectangle(Field.FieldBounds.FieldLeftX, Field.FieldBounds.FieldTopY, Field.FieldBounds.FieldRightX - Field.FieldBounds.FieldLeftX, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
        var grassColor = new Color(0, 120, 0);  // Tecmo green
        
        var texture = GetSolidTexture(grassColor);
        spriteBatch.Draw(texture, fieldRect, grassColor);
    }
    
    private void DrawYardLines(SpriteBatch spriteBatch)
    {
        var whiteTexture = GetSolidTexture(Color.White);
        
        // Draw yard lines every 10 yards
        for (int yard = 0; yard <= 100; yard += 10)
        {
            int x = YardToX(yard);
            var lineRect = new Rectangle(x, Field.FieldBounds.FieldTopY, 1, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
            spriteBatch.Draw(whiteTexture, lineRect, Color.White);
        }
        
        // Draw thicker goal lines
        int goalLine0 = YardToX(0);
        int goalLine100 = YardToX(100);
        
        spriteBatch.Draw(whiteTexture, new Rectangle(goalLine0 - 1, Field.FieldBounds.FieldTopY, 2, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY), Color.White);
        spriteBatch.Draw(whiteTexture, new Rectangle(goalLine100 - 1, Field.FieldBounds.FieldTopY, 2, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY), Color.White);
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
    
    private void DrawEndZones(SpriteBatch spriteBatch)
    {
        // Solid color end zones
        var endZoneColor = new Color(0, 80, 0);
        var texture = GetSolidTexture(endZoneColor);
        
        int goalLine0 = YardToX(0);
        int goalLine100 = YardToX(100);
        
        // Left end zone
        var leftEndZone = new Rectangle(Field.FieldBounds.FieldLeftX - Field.FieldBounds.EndZoneDepth, Field.FieldBounds.FieldTopY, Field.FieldBounds.EndZoneDepth, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
        spriteBatch.Draw(texture, leftEndZone, endZoneColor);
        
        // Right end zone
        var rightEndZone = new Rectangle(goalLine100, Field.FieldBounds.FieldTopY, Field.FieldBounds.EndZoneDepth, Field.FieldBounds.FieldBottomY - Field.FieldBounds.FieldTopY);
        spriteBatch.Draw(texture, rightEndZone, endZoneColor);
    }
    
    private int YardToX(int yard)
    {
        // Map 0-100 yards to field coordinates
        float yardWidth = (Field.FieldBounds.FieldRightX - Field.FieldBounds.FieldLeftX) / 100f;
        return Field.FieldBounds.FieldLeftX + (int)(yard * yardWidth);
    }
    
    private Texture2D GetSolidTexture(Color color)
    {
        var texture = new Texture2D(_graphicsDevice, 1, 1);
        texture.SetData(new[] { color });
        return texture;
    }
}
