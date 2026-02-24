using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class SpriteScriptYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SpriteScript LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<SpriteScriptYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class SpriteScriptYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<SpriteFrameYamlDto> Frames { get; set; } = new();
        public SpriteLoopYamlDto? Loop { get; set; }

        public SpriteScript ToModel()
        {
            return new SpriteScript(
                Id,
                Frames.Select(f => f.ToModel()).ToList(),
                Loop?.ToModel());
        }
    }

    private sealed class SpriteFrameYamlDto
    {
        public int Duration { get; set; }
        public List<SpritePieceYamlDto> Sprites { get; set; } = new();
        public SpriteOpYamlDto? Op { get; set; }

        public SpriteFrame ToModel()
        {
            return new SpriteFrame(
                Duration,
                Sprites.Select(s => s.ToModel()).ToList(),
                Op?.ToModel());
        }
    }

    private sealed class SpritePieceYamlDto
    {
        public string Tile { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Pal { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }

        public SpritePiece ToModel() => new(Tile, X, Y, Pal, FlipX, FlipY);
    }

    private sealed class SpriteOpYamlDto
    {
        public string Kind { get; set; } = string.Empty;
        public int Dx { get; set; }
        public int Dy { get; set; }

        public SpriteOp ToModel() => new(Kind, Dx, Dy);
    }

    private sealed class SpriteLoopYamlDto
    {
        public string Kind { get; set; } = string.Empty;
        public int Frame { get; set; }

        public SpriteLoop ToModel() => new(Kind, Frame);
    }
}
