using System.Text;
using System.Xml.Linq;
using CanadaRidingTagger;
using NetTopologySuite.IO.KML;

namespace CanadaRidingTagger.Readers;

/// <summary>
/// Reads a KML dataset. Coordinates are assumed to be WGS84 degrees and pass
/// through unchanged. Attributes are read from
/// <c>ExtendedData</c> (<c>Data</c>/<c>value</c> and <c>SimpleData</c> forms);
/// when absent, the placemark <c>&lt;name&gt;</c> is used as the district name.
/// </summary>
internal static class KmlDatasetReader
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";
    private static readonly string[] GeometryElements =
    {
        "Polygon", "MultiPolygon", "MultiGeometry", "LineString", "MultiLineString",
        "Point", "MultiPoint",
    };

    public static IReadOnlyList<DistrictGeometry> Read(DistrictSource source)
    {
        XDocument doc = Load(source.Path);
        var kmlReader = new KMLReader();
        var result = new List<DistrictGeometry>();

        foreach (XElement placemark in doc.Descendants(Kml + "Placemark"))
        {
            string? placemarkName = placemark.Element(Kml + "name")?.Value;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            XElement? extended = placemark.Element(Kml + "ExtendedData");
            if (extended is not null)
            {
                foreach (XElement data in extended.Elements(Kml + "Data"))
                {
                    if (data.Attribute("name")?.Value is string key)
                    {
                        attributes[key] = data.Element(Kml + "value")?.Value ?? "";
                    }
                }
                foreach (XElement simple in extended.Elements(Kml + "SimpleData"))
                {
                    if (simple.Attribute("name")?.Value is string key)
                    {
                        attributes[key] = simple.Value;
                    }
                }
            }

            string? Attr(string field)
            {
                return attributes.TryGetValue(field, out string? value) ? value : null;
            }

            District district = DistrictFactory.Extract(source.Definition, Attr, placemarkName);

            XElement? geometry = null;
            foreach (string name in GeometryElements)
            {
                geometry = placemark.Element(Kml + name);
                if (geometry is not null)
                {
                    break;
                }
            }
            if (geometry is null)
            {
                throw new FormatException($"Placemark '{placemarkName}' has no geometry.");
            }

            // NTS KMLReader parses a bare geometry element (local-name
            // matching, namespace-agnostic).
            string fragment = geometry.ToString();
            result.Add(new DistrictGeometry(district, kmlReader.Read(fragment)));
        }

        return result;
    }

    private static XDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        try
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            return XDocument.Parse(text);
        }
        catch (DecoderFallbackException)
        {
            return XDocument.Parse(Encoding.GetEncoding(1252).GetString(bytes));
        }
    }
}
