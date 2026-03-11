using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Rendering.UI;

namespace TecmoSBGame.Systems;

/// <summary>
/// Renders entities with Position and Sprite components.
///
/// NOTE: In this project SpriteBatch.Begin/End is managed by MainGame.Draw() so that
/// all renderers share the same transform matrix (virtual 256x224 scaling).
/// </summary>
public sealed class RenderingSystem : EntityDrawSystem
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    private ComponentMapper<PositionComponent> _positionMapper = null!;
    private ComponentMapper<SpriteComponent> _spriteMapper = null!;
    private ComponentMapper<TeamComponent> _teamMapper = null!;
    private ComponentMapper<PlayerRoleComponent> _roleMapper = null!;
    private ComponentMapper<PlayerAttributesComponent> _attrsMapper = null!;
    private ComponentMapper<BallCarrierComponent> _ballCarrierMapper = null!;
    private ComponentMapper<PlayerControlComponent> _controlMapper = null!;
    private ComponentMapper<BallComponent> _ballTagMapper = null!;

    public bool ShowLabels { get; set; } = true;

    public RenderingSystem(SpriteBatch spriteBatch, Texture2D pixel)
        : base(Aspect.All(typeof(PositionComponent), typeof(SpriteComponent)))
    {
        _spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _spriteMapper = mapperService.GetMapper<SpriteComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _roleMapper = mapperService.GetMapper<PlayerRoleComponent>();
        _attrsMapper = mapperService.GetMapper<PlayerAttributesComponent>();
        _ballCarrierMapper = mapperService.GetMapper<BallCarrierComponent>();
        _controlMapper = mapperService.GetMapper<PlayerControlComponent>();
        _ballTagMapper = mapperService.GetMapper<BallComponent>();
    }

    public override void Draw(GameTime gameTime)
    {
        var font = FontSystem.Instance.GetFont(FontSize.Small);

        foreach (var entityId in ActiveEntities)
        {
            var pos = _positionMapper.Get(entityId).Position;
            var sprite = _spriteMapper.Get(entityId);

            // Placeholder: draw a 16x16 marker at entity position.
            // Use Pixel + tinting (no per-frame texture allocations).
            var teamColor = GetTeamColor(entityId);
            var rect = new Rectangle((int)pos.X - 8, (int)pos.Y - 8, 16, 16);

            // Fill
            _spriteBatch.Draw(_pixel, rect, teamColor);

            // Outline for special states
            if (_ballCarrierMapper.Has(entityId) && _ballCarrierMapper.Get(entityId).HasBall)
                DrawOutline(rect, Color.Gold);
            if (_controlMapper.Has(entityId) && _controlMapper.Get(entityId).IsControlled)
                DrawOutline(new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), Color.White);

            // Labels (position/role) for debugging.
            if (ShowLabels && font is not null)
            {
                var label = BuildLabel(entityId);
                if (!string.IsNullOrEmpty(label))
                {
                    var textPos = new Vector2(rect.X + rect.Width + 2, rect.Y - 6);
                    _spriteBatch.DrawString(font, label, textPos, Color.White);
                }
            }
        }
    }

    private string BuildLabel(int entityId)
    {
        // Ball marker
        if (_ballTagMapper.Has(entityId))
            return "BALL";

        // Prefer PlayerRole/slot if present.
        string role = string.Empty;
        string slot = string.Empty;
        if (_roleMapper.Has(entityId))
        {
            var r = _roleMapper.Get(entityId);
            role = r.Role.ToString();
            slot = r.Slot ?? string.Empty;
        }

        // Prefer jersey/position if attributes exist.
        string attrs = string.Empty;
        if (_attrsMapper.Has(entityId))
        {
            var a = _attrsMapper.Get(entityId);
            var pos = (a.Position ?? string.Empty).Trim();
            var num = a.JerseyNumber;
            if (!string.IsNullOrEmpty(pos))
                attrs = num > 0 ? $"{pos}{num}" : pos;
        }

        var teamStr = _teamMapper.Has(entityId) ? _teamMapper.Get(entityId).TeamIndex.ToString() : "?";

        var core = !string.IsNullOrEmpty(attrs) ? attrs : role;
        if (!string.IsNullOrEmpty(slot) && slot.Length <= 4)
            core = string.IsNullOrEmpty(core) ? slot : $"{core}/{slot}";

        if (string.IsNullOrEmpty(core))
            core = entityId.ToString();

        return $"t{teamStr} {core}";
    }

    private void DrawOutline(Rectangle rect, Color color)
    {
        // Top
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        // Bottom
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), color);
        // Left
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        // Right
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), color);
    }

    private Color GetTeamColor(int entityId)
    {
        if (!_teamMapper.Has(entityId))
            return Color.White;

        var team = _teamMapper.Get(entityId);
        // TODO: Use actual team colors from GameContent
        return team.IsOffense ? new Color(40, 120, 255) : new Color(220, 50, 50);
    }
}
