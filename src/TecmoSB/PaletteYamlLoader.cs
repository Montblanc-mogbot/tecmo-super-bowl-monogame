using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TecmoSB;

public static class PaletteYamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static (IReadOnlyList<Palette> palettes, IReadOnlyList<PaletteCycle> cycles) LoadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var dto = Deserializer.Deserialize<PaletteSetDto>(yaml);

        var palettes = dto.Palettes.Select(p => new Palette(p.Id, p.Colors)).ToList();
        var cycles = dto.Cycles.Select(c => new PaletteCycle(c.Id, c.Frames, c.TicksPerFrame)).ToList();
        return (palettes, cycles);
    }

    private sealed class PaletteSetDto
    {
        public List<PaletteDto> Palettes { get; set; } = new();
        public List<PaletteCycleDto> Cycles { get; set; } = new();
    }

    private sealed class PaletteDto
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Colors { get; set; } = new();
    }

    private sealed class PaletteCycleDto
    {
        public string Id { get; set; } = string.Empty;
        public int TicksPerFrame { get; set; } = 8;
        public List<string> Frames { get; set; } = new();
    }
}
