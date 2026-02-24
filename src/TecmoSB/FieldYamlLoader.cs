using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FieldYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FieldConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FieldConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FieldConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string FieldLayoutId { get; set; } = string.Empty;
        public List<BallAnimationDefYamlDto> BallAnimations { get; set; } = new();
        public List<FieldBoundaryYamlDto> Boundaries { get; set; } = new();

        public FieldConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FieldConfig.id is required");

            if (string.IsNullOrWhiteSpace(FieldLayoutId))
                throw new InvalidDataException("FieldConfig.field_layout_id is required");

            var animMap = new Dictionary<string, BallAnimationDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in BallAnimations)
            {
                var model = a.ToModel();
                if (!animMap.TryAdd(model.Id, model))
                    throw new InvalidDataException($"Duplicate ball animation id: {model.Id}");
            }

            return new FieldConfig(
                Id,
                FieldLayoutId,
                animMap,
                Boundaries.Select(b => b.ToModel()).ToList());
        }
    }

    private sealed class BallAnimationDefYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public List<BallAnimationFrameYamlDto> Frames { get; set; } = new();
        public bool Loop { get; set; }

        public BallAnimationDef ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("BallAnimationDef.id is required");

            if (Frames.Count == 0)
                throw new InvalidDataException($"BallAnimationDef.frames must be non-empty (anim id '{Id}')");

            return new BallAnimationDef(
                Id,
                Frames.Select(f => f.ToModel()).ToList(),
                Loop);
        }
    }

    private sealed class BallAnimationFrameYamlDto
    {
        public string SpriteId { get; set; } = string.Empty;
        public int Duration { get; set; }

        public BallAnimationFrame ToModel()
        {
            if (string.IsNullOrWhiteSpace(SpriteId))
                throw new InvalidDataException("BallAnimationFrame.sprite_id is required");

            return new BallAnimationFrame(SpriteId, Duration);
        }
    }

    private sealed class FieldBoundaryYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public FieldBoundary ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FieldBoundary.id is required");

            if (string.IsNullOrWhiteSpace(Kind))
                throw new InvalidDataException($"FieldBoundary.kind is required (boundary id '{Id}')");

            return new FieldBoundary(Id, Kind, X, Y, Width, Height);
        }
    }
}
