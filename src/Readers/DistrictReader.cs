using CanadaRidingTagger;

namespace CanadaRidingTagger.Readers;

/// <summary>
/// Reads a district dataset (shapefile, GeoJSON, or KML) into
/// <see cref="DistrictGeometry"/> items in WGS84 degrees.
/// </summary>
internal static class DistrictReader
{
    /// <summary>
    /// Reads the dataset described by <paramref name="source"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="FormatException">The extension is unsupported.</exception>
    public static IReadOnlyList<DistrictGeometry> Read(DistrictSource source)
    {
        if (!File.Exists(source.Path))
        {
            throw new FileNotFoundException($"Dataset file not found: {source.Path}", source.Path);
        }

        string ext = Path.GetExtension(source.Path).ToLowerInvariant();
        switch (ext)
        {
            case ".shp":
                return ShapefileDatasetReader.Read(source);
            case ".geojson":
                return GeoJsonDatasetReader.Read(source);
            case ".kml":
                return KmlDatasetReader.Read(source);
            default:
                throw new FormatException($"Unsupported dataset extension: {ext}");
        }
    }
}
