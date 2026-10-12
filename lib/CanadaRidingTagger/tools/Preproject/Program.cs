using System.Globalization;
using System.Text;
using CanadaRidingTagger.Projections;
using CanadaRidingTagger.Readers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json.Linq;

// One-time tool: rewrites the bundled datasets as WGS84 shapefiles so the
// library can load them without applying a .prj CRS at runtime.
//   <file>.shp      coordinates inverse-projected through the sibling .prj
//                   (when present) to WGS84 degrees; every attribute column is
//                   preserved
//   <file>.geojson  data is already WGS84; written as <file>.shp/.dbf/.shx
// Attribute values are written as character columns in original column order,
// sized to the widest value so nothing truncates; column names longer than the
// DBF 10-character limit are truncated and disambiguated if needed.

internal static class Program
{
    private const int DbfNameLimit = 10;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: Preproject <file>...  (.shp or .geojson paths)");
            return 2;
        }

        int failures = 0;
        foreach (string path in args)
        {
            try
            {
                if (path.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase))
                {
                    ConvertGeoJson(path, Path.ChangeExtension(path, ".shp")!);
                }
                else
                {
                    ConvertShapefile(path, path);
                }
            }
            catch (Exception e)
            {
                failures++;
                Console.Error.WriteLine($"{path}: {e}");
                Console.Error.WriteLine(e.StackTrace);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static void ConvertShapefile(string shpPath, string targetPath)
    {
        var factory = new GeometryFactory();
        string prjPath = Path.ChangeExtension(shpPath, ".prj")!;
        IProjection? projection = File.Exists(prjPath)
            ? ProjectionFactory.Create(File.ReadAllText(prjPath))
            : null;
        var projector = projection is null ? null : new ShapefileDatasetReader.InverseProjector(projection);

        var features = new List<Feature>();
        var used = new HashSet<string>();
        using (var reader = new ShapefileDataReader(shpPath, factory, Encoding.UTF8))
        {
            while (reader.Read())
            {
                Geometry geometry = projector is null
                    ? reader.Geometry
                    : projector.Transform(reader.Geometry);
                var attributes = new AttributesTable();
                for (int i = 1; i < reader.FieldCount; i++)
                {
                    string name = AllocateName(reader.GetName(i), used);

                    object? value = reader.GetValue(i);
                    attributes.Add(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                }

                features.Add(new Feature(geometry, attributes));
            }
        }

        WriteShapefile(targetPath, factory, features);
        Console.WriteLine($"{targetPath}: {features.Count} features{(projection is null ? " (no .prj; WGS84 pass-through)" : $" (inverse-projected through {Path.GetFileName(prjPath)})")}");
    }

    private static void ConvertGeoJson(string geoJsonPath, string targetPath)
    {
        string json = File.ReadAllText(geoJsonPath);
        var source = new GeoJsonReader().Read<FeatureCollection>(json)
            ?? throw new FormatException("no FeatureCollection in " + geoJsonPath);
        JArray? featuresJson = JObject.Parse(json)["features"] as JArray;
        if (featuresJson is null || featuresJson.Count != source.Count)
        {
            throw new FormatException($"feature count mismatch in {geoJsonPath}");
        }

        // Column list: the union of every feature's properties in order of
        // first appearance; every feature gets every column ("" when absent).
        var columns = new List<string>();
        var columnIndex = new Dictionary<string, int>();
        var used = new HashSet<string>();
        foreach (JToken? feature in featuresJson)
        {
            JObject? properties = feature?["properties"] as JObject;
            if (properties is null)
            {
                continue;
            }

            foreach (JProperty property in properties.Properties())
            {
                if (columnIndex.TryAdd(property.Name, columns.Count))
                {
                    columns.Add(AllocateName(property.Name, used));
                }
            }
        }

        var features = new List<Feature>();
        int index = 0;
        foreach (IFeature item in source)
        {
            var values = new string[columns.Count];
            for (int c = 0; c < values.Length; c++)
            {
                values[c] = "";
            }

            JObject? properties = featuresJson[index++]?["properties"] as JObject;
            if (properties is not null)
            {
                foreach (JProperty property in properties.Properties())
                {
                    values[columnIndex[property.Name]] = ValueString(property.Value);
                }
            }

            var attributes = new AttributesTable();
            for (int c = 0; c < columns.Count; c++)
            {
                attributes.Add(columns[c], values[c]);
            }

            features.Add(new Feature(item.Geometry, attributes));
        }

        WriteShapefile(targetPath, new GeometryFactory(), features);
        Console.WriteLine($"{targetPath}: {features.Count} features (geojson; WGS84 pass-through)");
    }

    private static void WriteShapefile(string targetPath, GeometryFactory factory, List<Feature> features)
    {
        // The writer takes a base name (it appends .shp/.dbf/.shx itself), so
        // pass the final .shp path and it writes in place.
        var header = new DbaseFileHeader(Encoding.UTF8);
        if (features.Count > 0)
        {
            string[] names = features[0].Attributes.GetNames();
            for (int c = 0; c < names.Length; c++)
            {
                int width = 1;
                foreach (Feature feature in features)
                {
                    string value = feature.Attributes[names[c]] as string ?? "";
                    width = Math.Max(width, Encoding.UTF8.GetByteCount(value));
                }

                header.AddColumn(names[c], 'C', width, 0);
            }
        }

        var writer = new ShapefileDataWriter(targetPath, factory, Encoding.UTF8);
        writer.Header = header;
        writer.Write(features);
    }

    private static string AllocateName(string name, HashSet<string> used)
    {
        string baseName = name.Length <= DbfNameLimit ? name : name[..DbfNameLimit];
        if (used.Add(baseName))
        {
            return baseName;
        }

        // Collision after truncation: shorten to 9 characters + a digit.
        for (int n = 1; ; n++)
        {
            string candidate = name[..Math.Min(DbfNameLimit - 1, name.Length)] + n;
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string ValueString(JToken? value)
        => value is null || value.Type is JTokenType.Null or JTokenType.Undefined
            ? ""
            : value.ToString();
}
