using System.Text;
using CanadaRidingTagger;
using CanadaRidingTagger.Projections;
using CanadaRidingTagger.Readers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Reader tests over synthetic datasets (shapefile with a real
/// <c>.prj</c>, GeoJSON, KML). Real-data coverage is in the integration
/// tests.
/// </summary>
public class DistrictReaderTests
{
    private const double DegreeTolerance = 1e-6;
    private static readonly string FixturesRoot = LccFixtureTests.FindFixturesRoot();

    // --- shapefile -------------------------------------------------------

    [Fact]
    public void Shapefile_InverseProjectsThroughPrj()
    {
        string prjText = File.ReadAllText(Path.Combine(
            FixturesRoot, "prj",
            "ELECTORAL_DISTRICT.prj"));
        IProjection proj = ProjectionFactory.Create(prjText);

        const double latDeg = 45.42, lonDeg = -75.69;
        const double Deg = Math.PI / 180.0;
        proj.Forward(latDeg * Deg, lonDeg * Deg, out double x, out double y);
        var ring = new[]
        {
            new Coordinate(x - 1000, y - 1000),
            new Coordinate(x + 1000, y - 1000),
            new Coordinate(x + 1000, y + 1000),
            new Coordinate(x - 1000, y + 1000),
            new Coordinate(x - 1000, y - 1000),
        };
        var ring2 = new[]
        {
            new Coordinate(x + 5000, y + 5000),
            new Coordinate(x + 7000, y + 5000),
            new Coordinate(x + 7000, y + 7000),
            new Coordinate(x + 5000, y + 7000),
            new Coordinate(x + 5000, y + 5000),
        };

        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "test.shp");
        WriteShapefile(path, new[]
        {
            ("RIDING_ID", 'C', 10), ("NAME_EN", 'C', 30), ("NAME_FR", 'C', 30),
        }, new (Geometry, Dictionary<string, object>)[]
        {
            (new Polygon(new LinearRing(ring)), new Dictionary<string, object>
            {
                ["RIDING_ID"] = "1", ["NAME_EN"] = "Test North", ["NAME_FR"] = "Nord",
            }),
            (new Polygon(new LinearRing(ring2)), new Dictionary<string, object>
            {
                ["RIDING_ID"] = "2", ["NAME_EN"] = "Test East",
            }),
        });
        File.WriteAllText(Path.ChangeExtension(path, ".prj"), prjText);

        var districts = DistrictReader.Read(new DistrictSource(
            path, new DatasetDefinition("RIDING_ID", "NAME_EN", "NAME_FR"),
            Jurisdiction.Ontario));

        Assert.Equal(2, districts.Count);
        Assert.Equal("1", districts[0].District.Id);
        Assert.Equal("Test North", districts[0].District.NameEn);
        Assert.Equal("Nord", districts[0].District.NameFr);
        Assert.Null(districts[1].District.NameFr);

        // A projected square's inverse is not a geodesic square (LCC
        // second-order term), so check a per-corner round trip: forward the
        // read-back corners and compare against the written corners (meters).
        // NTS's ShapefileDataWriter writes exterior shells clockwise, so our
        // CCW ring is stored reversed. This test checks projection round-trip
        // accuracy, not ring orientation, so match each read corner to its
        // nearest written corner.
        var readRing = ((Polygon)districts[0].Geometry).ExteriorRing.Coordinates;
        Assert.True(readRing.Length >= 4,
            $"expected a closed square ring, got {readRing.Length} corners");
        foreach (Coordinate read in readRing)
        {
            proj.Forward(read.Y * Deg, read.X * Deg, out double bx, out double by);
            double best = double.MaxValue;
            foreach (Coordinate written in ring)
                best = Math.Min(best, Sq(new Coordinate(bx, by), written));
            Assert.True(Math.Sqrt(best) < 0.01,
                $"read corner ({read.X} {read.Y}) -> ({bx} {by}); " +
                $"no written corner within 0.01 m");
        }
    }

    [Fact]
    public void Shapefile_NumericIdField_IsWholeNumber()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "test.shp");
        WriteShapefile(path, new[]
        {
            ("RIDING_ID", 'N', 10), ("NAME_EN", 'C', 30),
        }, new (Geometry, Dictionary<string, object>)[]
        {
            (new Polygon(new LinearRing(Square(1, 2))), new Dictionary<string, object>
            {
                ["RIDING_ID"] = 33, ["NAME_EN"] = "Numeric",
            }),
        });
        // No .prj: assume WGS84 degrees.

        var districts = DistrictReader.Read(new DistrictSource(
            path, new DatasetDefinition("RIDING_ID", "NAME_EN", null), null));

        Assert.Equal("33", districts[0].District.Id);
        Assert.Equal("Numeric", districts[0].District.NameEn);
    }

    // --- GeoJSON ----------------------------------------------------------

    [Fact]
    public void GeoJson_ReadsFeatures()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "test.geojson");
        File.WriteAllText(path, """
        {"type":"FeatureCollection","features":[
         {"type":"Feature",
          "properties":{"RIDING_ID":"1","NAME_EN":"Geo North","NAME_FR":"Nord"},
          "geometry":{"type":"Polygon","coordinates":[[[-75.0,45.0],[-75.0,46.0],[-74.0,46.0],[-74.0,45.0],[-75.0,45.0]]]}},
         {"type":"Feature",
          "properties":{"RIDING_ID":"2","NAME_EN":"Geo South"},
          "geometry":{"type":"Polygon","coordinates":[[[-75.0,43.0],[-75.0,44.0],[-74.0,44.0],[-74.0,43.0],[-75.0,43.0]]]}}
        ]}
        """);

        var districts = DistrictReader.Read(new DistrictSource(
            path, new DatasetDefinition("RIDING_ID", "NAME_EN", "NAME_FR"), null));

        Assert.Equal(2, districts.Count);
        Assert.Equal("1", districts[0].District.Id);
        Assert.Equal("Geo North", districts[0].District.NameEn);
        Assert.Equal("Nord", districts[0].District.NameFr);
        Assert.Equal("2", districts[1].District.Id);
        Assert.Null(districts[1].District.NameFr);

        Coordinate c = EnvelopeCenter(districts[0].Geometry);
        Assert.True(Math.Abs(c.X - (-74.5)) < DegreeTolerance);
        Assert.True(Math.Abs(c.Y - 45.5) < DegreeTolerance);
    }

    // --- KML --------------------------------------------------------------

    [Fact]
    public void Kml_ReadsDataForm_SimpleDataForm_NameFallback_AndMultiGeometry()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "test.kml");
        File.WriteAllText(path, """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2">
         <Document>
          <Placemark>
           <name>Data Form North</name>
           <ExtendedData>
            <Data name="RIDING_ID"><value>77</value></Data>
            <Data name="NAME_EN"><value>Data Form North</value></Data>
            <Data name="NAME_FR"><value>Nord Formule</value></Data>
           </ExtendedData>
           <Polygon><outerBoundaryIs><LinearRing><coordinates>
            -75.0,45.0,0
            -75.0,46.0,0
            -74.0,46.0,0
            -75.0,45.0,0
           </coordinates></LinearRing></outerBoundaryIs></Polygon>
          </Placemark>
          <Placemark>
           <ExtendedData>
            <SimpleData name="NAME_EN">South Plain</SimpleData>
           </ExtendedData>
           <MultiGeometry>
            <Polygon><outerBoundaryIs><LinearRing><coordinates>
             -75.0,43.0,0
             -75.0,44.0,0
             -74.0,44.0,0
             -75.0,43.0,0
            </coordinates></LinearRing></outerBoundaryIs></Polygon>
            <Polygon><outerBoundaryIs><LinearRing><coordinates>
             -73.0,43.0,0
             -73.0,44.0,0
             -72.0,44.0,0
             -73.0,43.0,0
            </coordinates></LinearRing></outerBoundaryIs></Polygon>
           </MultiGeometry>
          </Placemark>
          <Placemark>
           <name><![CDATA[NameOnly CDATA]]></name>
           <Polygon><outerBoundaryIs><LinearRing><coordinates>
            -75.0,41.0,0
            -75.0,42.0,0
            -74.0,42.0,0
            -75.0,41.0,0
           </coordinates></LinearRing></outerBoundaryIs></Polygon>
          </Placemark>
         </Document>
        </kml>
        """);

        var districts = DistrictReader.Read(new DistrictSource(
            path, new DatasetDefinition("RIDING_ID", "NAME_EN", "NAME_FR"), null));

        Assert.Equal(3, districts.Count);

        Assert.Equal("77", districts[0].District.Id);
        Assert.Equal("Data Form North", districts[0].District.NameEn);
        Assert.Equal("Nord Formule", districts[0].District.NameFr);
        Assert.IsType<Polygon>(districts[0].Geometry);

        // No id field value: id falls back to the district name.
        Assert.Equal("South Plain", districts[1].District.Id);
        Assert.Equal("South Plain", districts[1].District.NameEn);
        Assert.Null(districts[1].District.NameFr);
        Assert.IsType<MultiPolygon>(districts[1].Geometry);

        // No ExtendedData at all: <name> is the district name.
        Assert.Equal("NameOnly CDATA", districts[2].District.Id);
        Assert.Equal("NameOnly CDATA", districts[2].District.NameEn);
        Assert.IsType<Polygon>(districts[2].Geometry);
    }

    // --- dispatcher --------------------------------------------------------

    [Fact]
    public void Dispatcher_MissingFile_ThrowsFileNotFoundException()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "missing.shp");
        var ex = Assert.Throws<FileNotFoundException>(() => DistrictReader.Read(
            new DistrictSource(path,
                new DatasetDefinition("RIDING_ID", "NAME_EN", null), null)));
        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public void Dispatcher_UnsupportedExtension_ThrowsFormatException()
    {
        var dir = Directory.CreateTempSubdirectory();
        string path = Path.Combine(dir.FullName, "missing.xyz");
        File.WriteAllText(path, "junk");
        Assert.Throws<FormatException>(() => DistrictReader.Read(
            new DistrictSource(path,
                new DatasetDefinition("RIDING_ID", "NAME_EN", null), null)));
    }

    // --- helpers ------------------------------------------------------------

    private static Coordinate EnvelopeCenter(Geometry geometry)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (Coordinate c in geometry.Envelope.Coordinates)
        {
            if (c.X < minX) minX = c.X;
            if (c.Y < minY) minY = c.Y;
            if (c.X > maxX) maxX = c.X;
            if (c.Y > maxY) maxY = c.Y;
        }
        return new Coordinate((minX + maxX) / 2, (minY + maxY) / 2);
    }

    private static Coordinate[] Square(double x, double y)
    {
        return new[]
        {
            new Coordinate(x, y),
            new Coordinate(x + 1, y),
            new Coordinate(x + 1, y + 1),
            new Coordinate(x, y + 1),
            new Coordinate(x, y),
        };
    }

    private static double Sq(Coordinate a, Coordinate b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static void WriteShapefile(
        string path,
        (string Name, char Type, int Length)[] fields,
        (Geometry Geometry, Dictionary<string, object> Attrs)[] districts)
    {
        var factory = new GeometryFactory();
        var writer = new ShapefileDataWriter(path, factory, Encoding.UTF8);
        var header = new DbaseFileHeader(Encoding.UTF8);
        foreach ((string name, char type, int length) in fields)
        {
            header.AddColumn(name, type, length, 0);
        }
        writer.Header = header;

        var features = new Feature[districts.Length];
        for (int i = 0; i < districts.Length; i++)
        {
            var attributes = new AttributesTable();
            foreach ((string key, object value) in districts[i].Attrs)
            {
                attributes.Add(key, value);
            }
            foreach ((string name, _, _) in fields)
            {
                if (!attributes.Exists(name))
                {
                    attributes.Add(name, null);
                }
            }
            features[i] = new Feature(districts[i].Geometry, attributes);
        }
        writer.Write(features);
    }
}
