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
/// Elements are matched by local name, so exports in any KML namespace load.
/// </summary>
internal static class KmlDatasetReader
{
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

        foreach (XElement placemark in doc.Descendants().Where(e => e.Name.LocalName == "Placemark"))
        {
            string? placemarkName = Element(placemark, "name")?.Value;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            XElement? extended = Element(placemark, "ExtendedData");
            if (extended is not null)
            {
                foreach (XElement data in extended.Descendants().Where(e => e.Name.LocalName == "Data"))
                {
                    if (data.Attribute("name")?.Value is string key)
                    {
                        attributes[key] = Element(data, "value")?.Value ?? "";
                    }
                }
                foreach (XElement simple in extended.Descendants().Where(e => e.Name.LocalName == "SimpleData"))
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

            // Poll-division exports (e.g. PEI) include unassigned-area placemarks
            // with empty names; they are not district boundaries, so skip them.
            if (DistrictFactory.Clean(Attr(source.Definition.NameEn)) is null &&
                DistrictFactory.Clean(placemarkName) is null)
            {
                continue;
            }

            District district = DistrictFactory.Extract(source.Definition, Attr, placemarkName);

            // Polygons sit directly under the placemark or are grouped in a
            // MultiGeometry/GeometryCollection, depending on the export:
            // gather all of them, combining into a MultiGeometry when there
            // is more than one.
            var polygons = placemark.Descendants()
                .Where(e => e.Name.LocalName == "Polygon")
                .ToList();
            string fragment;
            if (polygons.Count == 1)
            {
                fragment = polygons[0].ToString();
            }
            else if (polygons.Count > 1)
            {
                fragment = "<MultiGeometry>" + string.Concat(polygons.Select(p => p.ToString())) + "</MultiGeometry>";
            }
            else
            {
                XElement? geometry = null;
                foreach (string name in GeometryElements)
                {
                    geometry = Element(placemark, name);
                    if (geometry is not null)
                    {
                        break;
                    }
                }
                if (geometry is null)
                {
                    throw new FormatException($"Placemark '{placemarkName}' has no geometry.");
                }
                fragment = geometry.ToString();
            }

            // NTS KMLReader parses a bare geometry element (local-name
            // matching, namespace-agnostic).
            result.Add(new DistrictGeometry(district, kmlReader.Read(fragment)));
        }

        return result;
    }

    // A direct child matched by local name, so exports in any KML namespace
    // (e.g. http://www.opengis.net/kml/2.2, http://earth.google.com/kml/2.0) load.
    private static XElement? Element(XElement parent, string localName)
    {
        foreach (XElement element in parent.Elements())
        {
            if (element.Name.LocalName == localName)
            {
                return element;
            }
        }
        return null;
    }

    private static readonly Encoding Cp1252 = GetCp1252();

    // On the desktop .NET runtime, code-page encodings are only available once
    // a CodePagesEncodingProvider is registered (Mono/Android ship them built in).
    private static Encoding GetCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static XDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return XDocument.Parse(Decode(bytes));
    }

    // KML exports declare UTF-8, but some (e.g. MapInfo exports) store individual
    // non-ASCII characters as single code-page-1252 bytes. Decode as UTF-8,
    // interpreting each invalid byte as code page 1252, so mixed-encoding files
    // load without mojibake.
    private static string Decode(byte[] bytes)
    {
        int start = HasUtf8Bom(bytes) ? 3 : 0;
        var text = new StringBuilder(bytes.Length - start);
        for (int i = start; i < bytes.Length;)
        {
            int length = ValidUtf8Length(bytes, i);
            if (length is 2 or 3 or 4)
            {
                text.Append(Encoding.UTF8.GetString(bytes, i, length));
                i += length;
            }
            else
            {
                text.Append(Cp1252.GetString(bytes, i, 1));
                i += 1;
            }
        }
        return text.ToString();
    }

    private static bool HasUtf8Bom(byte[] b) =>
        b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;

    // The length of the well-formed UTF-8 sequence starting at i (1 for an ASCII
    // byte), or 0 when the sequence is ill-formed.
    private static int ValidUtf8Length(byte[] b, int i)
    {
        byte lead = b[i];
        if (lead < 0x80)
        {
            return 1;
        }
        int length = lead switch
        {
            >= 0xC2 and < 0xE0 => 2,
            >= 0xE0 and < 0xF0 => 3,
            >= 0xF0 and <= 0xF4 => 4,
            _ => 0,
        };
        if (i + length > b.Length)
        {
            return 0;
        }
        for (int k = 1; k < length; k++)
        {
            if ((b[i + k] & 0xC0) != 0x80)
            {
                return 0;
            }
        }
        return length;
    }
}
