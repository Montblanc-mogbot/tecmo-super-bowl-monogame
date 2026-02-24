namespace TecmoSB;

/// <summary>
/// YAML-driven field layout data.
///
/// Based on DOCS/FIELDLAYOUT.xlsx - describes how the football field
/// is constructed from tiles, including zoom-out points and subsections.
/// </summary>
public sealed record FieldLayoutConfig(
    string Id,
    FieldZoomOutPointers FieldZoomOutPointers,
    FullFieldTiles FullFieldTiles,
    FieldSubsections FieldSubsections,
    FieldSubsectionLookup FieldSubsectionLookup,
    IReadOnlyList<ZoomLevel> ZoomLevels,
    FieldDimensions FieldDimensions,
    IReadOnlyList<YardLinePosition> YardLinePositions,
    IReadOnlyList<string> Notes);

public sealed record FieldZoomOutPointers(
    string Description,
    IReadOnlyList<FieldSection> Sections);

public sealed record FieldSection(
    string Id,
    string Name,
    string Description);

public sealed record FullFieldTiles(
    string Description,
    int TileSizePixels,
    int FieldWidthTiles,
    int FieldHeightTiles,
    IReadOnlyList<string> TileTypes);

public sealed record FieldSubsections(
    string Description,
    IReadOnlyList<SubsectionRow> Rows);

public sealed record SubsectionRow(
    string Id,
    string Description);

public sealed record FieldSubsectionLookup(
    string Description);

public sealed record ZoomLevel(
    string Id,
    string Description,
    int VisibleFieldWidth,
    int VisibleFieldHeight,
    double TileScale);

public sealed record FieldDimensions(
    int YardsLong,
    int YardsWide,
    int PixelsPerYardX,
    int PixelsPerYardY,
    int TotalWidthPixels,
    int TotalHeightPixels);

public sealed record YardLinePosition(
    int Yard,
    int TileX,
    string Label);
