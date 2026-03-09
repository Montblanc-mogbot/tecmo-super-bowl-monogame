using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Renders entities with Position and Sprite components.
/// </summary>
public class RenderingSystem : EntityDrawSystem
{
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private ComponentMapper<PositionComponent> _positionMapper;
    private ComponentMapper<SpriteComponent> _spriteMapper;
    private ComponentMapper<TeamComponent> _teamMapper;

    public RenderingSystem(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice) 
        : base(Aspect.All(typeof(PositionComponent), typeof(SpriteComponent)))
    {
        _spriteBatch = spriteBatch;
        _graphicsDevice = graphicsDevice;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _spriteMapper = mapperService.GetMapper<SpriteComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
    }

    public override void Draw(GameTime gameTime)
    {
        // NOTE: SpriteBatch.Begin() was already called by MainGame.Draw()
        // Do NOT call Begin/End here - just issue draw commands
        
        foreach (var entityId in ActiveEntities)
        {
            var position = _positionMapper.Get(entityId);
            var sprite = _spriteMapper.Get(entityId);
            
            // TODO: Get actual texture from sprite.SpriteId
            // For now, draw a placeholder rectangle
            var color = GetTeamColor(entityId);
            var rect = new Rectangle((int)position.Position.X, (int)position.Position.Y, 16, 16);
            
            _spriteBatch.Draw(
                GetPlaceholderTexture(color),
                rect,
                null,
                sprite.Tint,
                sprite.Rotation,
                new Vector2(8, 8),
                sprite.FlipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0);
        }
        
        // NOTE: Do NOT call _spriteBatch.End() here - MainGame.Draw() handles it
    }
    
    private Color GetTeamColor(int entityId)
    {
        if (!_teamMapper.Has(entityId))
            return Color.White;
            
        var team = _teamMapper.Get(entityId);
        // TODO: Use actual team colors from GameContent
        return team.IsOffense ? Color.Blue : Color.Red;
    }
    
    private Texture2D GetPlaceholderTexture(Color color)
    {
        // TODO: Cache this
        var texture = new Texture2D(_graphicsDevice, 1, 1);
        texture.SetData(new[] { color });
        return texture;
    }
}
