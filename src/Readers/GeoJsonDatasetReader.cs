using CanadaRidingTagger;
using NetTopologySuite.Features;
using NetTopologySuite.IO;

namespace CanadaRidingTagger.Readers;

/// <summary>
/// Reads a GeoJSON FeatureCollection dataset. Coordinates are assumed to be
/// WGS84 degrees (GeoJSON spec) and pass through unchanged.
/// </summary>
internal static class GeoJsonDatasetReader
{
    public static IReadOnlyList<DistrictGeometry> Read(DistrictSource source)
    {
        string json = File.ReadAllText(source.Path);
        var reader = new GeoJsonReader();
        FeatureCollection? features = reader.Read<FeatureCollection>(json);
        if (features is null)
        {
            throw new FormatException($"No features found in {source.Path}.");
        }

        var result = new List<DistrictGeometry>();
        foreach (IFeature feature in features)
        {
            string? Attr(string field)
            {
                if (!feature.Attributes.Exists(field))
                {
                    return null;
                }
                return feature.Attributes[field] is object v && v.ToString() is string s ? s : null;
            }

            District district = DistrictFactory.Extract(source.Definition, Attr, null);
            result.Add(new DistrictGeometry(district, feature.Geometry));
        }

        return result;
    }
}
