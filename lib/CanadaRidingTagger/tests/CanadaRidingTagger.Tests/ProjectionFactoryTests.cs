using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Builds each of the 13 projections from the real <c>.prj</c> file and
/// validates the parser + parameter wiring against the pyproj 3.8.0 /
/// PROJ 9.8.1 forward grid in <c>Fixtures/&lt;key&gt;/proj.csv</c>.
/// The two bare geographic datasets (BC, NU) have no projected grid;
/// their identity behavior is checked directly.
/// </summary>
public class ProjectionFactoryTests
{
    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;
    private const double RoundTripRad = 1e-8;

    private static readonly string FixturesRoot = LccFixtureTests.FindFixturesRoot();
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir,
                    "Shapefiles", "Federal", "SHP")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Repo root not found");
    }

    private static IProjection Create(string prjRelPath)
    {
        return ProjectionFactory.Create(File.ReadAllText(Path.Combine(
            RepoRoot, "tests", "CanadaRidingTagger.Tests", "Fixtures", "prj",
            Path.GetFileName(prjRelPath))));
    }

    private static void CheckGrid(IProjection proj, string key)
    {
        double maxDx = 0, maxDy = 0;
        foreach (string line in File.ReadAllLines(
                Path.Combine(FixturesRoot, key, "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture);
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture);
            double rx = double.Parse(p[2], CultureInfo.InvariantCulture);
            double ry = double.Parse(p[3], CultureInfo.InvariantCulture);

            proj.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    private static void CheckRoundTrip(IProjection proj, string key)
    {
        foreach (string line in File.ReadAllLines(
                Path.Combine(FixturesRoot, key, "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture) * Deg;
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture) * Deg;
            proj.Forward(lat, lon, out double x, out double y);
            proj.Inverse(x, y, out double latBack, out double lonBack);
            Assert.True(Math.Abs(latBack - lat) < RoundTripRad,
                $"lat {latBack} vs {lat}");
            Assert.True(Math.Abs(lonBack - lon) < RoundTripRad,
                $"lon {lonBack} vs {lon}");
        }
    }

    [Theory]
    [InlineData("ab", "Shapefiles/Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.prj")]
    [InlineData("federal", "Shapefiles/Federal/SHP/FED_CA_2025_EN.prj")]
    [InlineData("mb", "Shapefiles/Provincial/MB/VA_MB2023_public.prj")]
    [InlineData("nb", "Shapefiles/Provincial/NB/Prov_PED_2024.prj")]
    [InlineData("ns", "Shapefiles/Provincial/NS/NS_2026ED_Bnds.prj")]
    [InlineData("on",
        "Shapefiles/Provincial/ON/Electoral District Shapefile - 2022 " +
        "General Election/ELECTORAL_DISTRICT.prj")]
    [InlineData("pei",
        "Shapefiles/Provincial/PEI/Electoral_District_Boundaries.prj")]
    [InlineData("qc",
        "Shapefiles/Provincial/QC/circonscriptions_électorales_2026_shapefile/" +
        "Circonscription_electorales_2026_shapefile.prj")]
    [InlineData("sk", "Shapefiles/Provincial/SK/ConstituencyGE30th.prj")]
    [InlineData("yt", "Shapefiles/Territorial/YT/Yukon_Electoral_Districts.prj")]
    [InlineData("nwt", "Shapefiles/Territorial/NWT/ElectoralDistricts.prj")]
    public void ForwardMatchesPyproj(string key, string prjRelPath)
    {
        CheckGrid(Create(prjRelPath), key);
    }

    [Theory]
    [InlineData("on",
        "Shapefiles/Provincial/ON/Electoral District Shapefile - 2022 " +
        "General Election/ELECTORAL_DISTRICT.prj")]
    [InlineData("mb", "Shapefiles/Provincial/MB/VA_MB2023_public.prj")]
    [InlineData("nb", "Shapefiles/Provincial/NB/Prov_PED_2024.prj")]
    [InlineData("ns", "Shapefiles/Provincial/NS/NS_2026ED_Bnds.prj")]
    [InlineData("ab", "Shapefiles/Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.prj")]
    [InlineData("federal", "Shapefiles/Federal/SHP/FED_CA_2025_EN.prj")]
    [InlineData("pei",
        "Shapefiles/Provincial/PEI/Electoral_District_Boundaries.prj")]
    [InlineData("qc",
        "Shapefiles/Provincial/QC/circonscriptions_électorales_2026_shapefile/" +
        "Circonscription_electorales_2026_shapefile.prj")]
    [InlineData("sk", "Shapefiles/Provincial/SK/ConstituencyGE30th.prj")]
    [InlineData("yt", "Shapefiles/Territorial/YT/Yukon_Electoral_Districts.prj")]
    [InlineData("nwt", "Shapefiles/Territorial/NWT/ElectoralDistricts.prj")]
    public void ForwardInverseRoundTrip(string key, string prjRelPath)
    {
        CheckRoundTrip(Create(prjRelPath), key);
    }

    [Theory]
    [InlineData("bc",
        "Shapefiles/Provincial/BC/EBC_ELECTORAL_DISTS_BS11_SVW/" +
        "EBC_ED_23_polygon.prj")]
    [InlineData("nu", "Shapefiles/Territorial/NU/EN_FUTURE_NU_Constituencies.prj")]
    public void BareGeographicIsDegreeIdentity(string key, string prjRelPath)
    {
        IProjection proj = Create(prjRelPath);
        double lat = 54.0 * Deg;
        double lon = -98.0 * Deg;
        proj.Forward(lat, lon, out double x, out double y);
        Assert.Equal(-98.0, x, 10);
        Assert.Equal(54.0, y, 10);
        proj.Inverse(x, y, out double latBack, out double lonBack);
        Assert.Equal(lat, latBack, 10);
        Assert.Equal(lon, lonBack, 10);
    }

    [Fact]
    public void UnknownProjectionThrows()
    {
        string wkt =
            "PROJCS[\"Test\",GEOGCS[\"GCS_Test\",DATUM[\"Test_Datum\"," +
            "SPHEROID[\"Test\",6378137.0,298.257222101]]," +
            "PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\"," +
            "0.017453292519943295]]," +
            "PROJECTION[\"Foobar\"],UNIT[\"Meter\",1.0]]";
        Assert.Throws<FormatException>(
            () => ProjectionFactory.Create(wkt));
    }

    [Fact]
    public void UnknownCrsTypeThrows()
    {
        string wkt =
            "COMPDATUM[\"Test\",GEOGCS[\"GCS_Test\"," +
            "PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\"," +
            "0.017453292519943295]],PROJCS[\"Test\",UNIT[\"Meter\",1.0]]]";
        Assert.Throws<FormatException>(
            () => ProjectionFactory.Create(wkt));
    }

    [Fact]
    public void MalformedWktThrows()
    {
        Assert.Throws<FormatException>(
            () => PrjParser.Parse("PROJCS[\"A\" \"B\"]"));
        Assert.Throws<FormatException>(
            () => PrjParser.Parse("PROJCS[\"A\"] junk"));
        Assert.Throws<FormatException>(
            () => PrjParser.Parse("PROJCS[\"A\", \"B\"] junk"));
    }
}
