using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class FieldLayoutYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static FieldLayoutConfig LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<FieldLayoutConfigYamlDto>(yaml);
        return dto.ToModel();
    }

    private sealed class FieldLayoutConfigYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public FieldZoomOutPointersYamlDto FieldZoomOutPointers { get; set; } = new();
        public FullFieldTilesYamlDto FullFieldTiles { get; set; } = new();
        public FieldSubsectionsYamlDto FieldSubsections { get; set; } = new();
        public FieldSubsectionLookupYamlDto FieldSubsectionLookup { get; set; } = new();
        public List<ZoomLevelYamlDto> ZoomLevels { get; set; } = new();
        public FieldDimensionsYamlDto FieldDimensions { get; set; } = new();
        public List<YardLinePositionYamlDto> YardLinePositions { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        public FieldLayoutConfig ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FieldLayoutConfig.id is required");

            return new FieldLayoutConfig(
                Id,
                FieldZoomOutPointers.ToModel(),
                FullFieldTiles.ToModel(),
                FieldSubsections.ToModel(),
                FieldSubsectionLookup.ToModel(),
                ZoomLevels.Select(z => z.ToModel()).ToList(),
                FieldDimensions.ToModel(),
                YardLinePositions.Select(y => y.ToModel()).ToList(),
                Notes.ToList());
        }
    }

    private sealed class FieldZoomOutPointersYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public List<FieldSectionYamlDto> Sections { get; set; } = new();

        public FieldZoomOutPointers ToModel() => new(
            Description ?? string.Empty,
            Sections.Select(s => s.ToModel()).ToList());
    }

    private sealed class FieldSectionYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public FieldSection ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("FieldSection.id is required");
            return new FieldSection(Id, Name ?? string.Empty, Description ?? string.Empty);
        }
    }

    private sealed class FullFieldTilesYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public int TileSizePixels { get; set; } = 8;
        public int FieldWidthTiles { get; set; } = 32;
        public int FieldHeightTiles { get; set; } = 16;
        public List<string> TileTypes { get; set; } = new();

        public FullFieldTiles ToModel() => new(
            Description ?? string.Empty,
            TileSizePixels,
            FieldWidthTiles,
            FieldHeightTiles,
            TileTypes.ToList());
    }

    private sealed class FieldSubsectionsYamlDto
    {
        public string Description { get; set; } = string.Empty;
        public List<SubsectionRowYamlDto> Rows { get; set; } = new();

        public FieldSubsections ToModel() => new(
            Description ?? string.Empty,
            Rows.Select(r => r.ToModel()).ToList());
    }

    private sealed class SubsectionRowYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public SubsectionRow ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("SubsectionRow.id is required");
            return new SubsectionRow(Id, Description ?? string.Empty);
        }
    }

    private sealed class FieldSubsectionLookupYamlDto
    {
        public string Description { get; set; } = string.Empty;

        public FieldSubsectionLookup ToModel() => new(Description ?? string.Empty);
    }

    private sealed class ZoomLevelYamlDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int VisibleFieldWidth { get; set; } = 16;
        public int VisibleFieldHeight { get; set; } = 12;
        public double TileScale { get; set; } = 1.0;

        public ZoomLevel ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new InvalidDataException("ZoomLevel.id is required");
            return new ZoomLevel(Id, Description ?? string.Empty, VisibleFieldWidth, VisibleFieldHeight, TileScale);
        }
    }

    private sealed class FieldDimensionsYamlDto
    {
        public int YardsLong { get; set; } = 100;
        public int YardsWide { get; set; } = 53;
        public int PixelsPerYardX { get; set; } = 8;
        public int PixelsPerYardY { get; set; } = 4;
        public int TotalWidthPixels { get; set; } = 424;
        public int TotalHeightPixels { get; set; } = 400;

        public FieldDimensions ToModel() => new(YardsLong, YardsWide, PixelsPerYardX, PixelsPerYardY, TotalWidthPixels, TotalHeightPixels);
    }

    private sealed class YardLinePositionYamlDto
    {
        public int Yard { get; set; } = 0;
        public int TileX { get; set; } = 0;
        public string Label { get; set; } = string.Empty;

        public YardLinePosition ToModel() => new(Yard, TileX, Label ?? string.Empty);
    }
}
