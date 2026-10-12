using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class LccFixtureTests
{
    private const double a = 6378137.0;
    private const double f = 1.0 / 298.257222101;
    private static readonly double e2 = 2 * f - f * f;
    private static readonly double e = Math.Sqrt(e2);
    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;

    private static readonly string FixturesRoot = FindFixturesRoot();

    // CRS per dataset: .tmp_fixtures/gen_fixtures.py (authoritative).
    // All lcc datasets: WGS84 ellipsoid above, k0 = 1.
    private static Lcc Make(double lat1Deg, double lat2Deg, double lat0Deg,
                            double lon0Deg, double x0, double y0)
    {
        return new Lcc(a, e2, e, 1.0,
            lat1Deg * Deg, lat2Deg * Deg, lat0Deg * Deg, lon0Deg * Deg,
            x0, y0);
    }

    internal static string FindFixturesRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "Fixtures");
            if (File.Exists(Path.Combine(candidate, "ab", "proj.csv")))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Fixtures not found");
    }

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 forward projection of the same
    // WGS84 grid, meters. proj.csv rows: lat,lon,x,y.
    [Theory]
    [InlineData("federal", 49, 77, 63.390675, -91.86666666666666, 6200000, 3000000)]
    [InlineData("on", 44.5, 54.5, 0, -84, 1000000, 0)]
    [InlineData("qc", 46, 50, 44, -70, 800000, 0)]
    [InlineData("nwt", 50, 70, 40, -96, 0, 0)]
    public void ForwardMatchesPyproj(string key, double lat1Deg, double lat2Deg,
                                      double lat0Deg, double lon0Deg,
                                      double x0, double y0)
    {
        Lcc lcc = Make(lat1Deg, lat2Deg, lat0Deg, lon0Deg, x0, y0);
        double maxDx = 0, maxDy = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, key, "proj.csv")))
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

            lcc.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    // Forward then inverse returns the input point at interior grid points
    // of each lcc dataset.
    [Theory]
    [InlineData("federal", 49, 77, 63.390675, -91.86666666666666, 6200000, 3000000)]
    [InlineData("on", 44.5, 54.5, 0, -84, 1000000, 0)]
    [InlineData("qc", 46, 50, 44, -70, 800000, 0)]
    [InlineData("nwt", 50, 70, 40, -96, 0, 0)]
    public void ForwardInverseRoundTrip(string key, double lat1Deg, double lat2Deg,
                                         double lat0Deg, double lon0Deg,
                                         double x0, double y0)
    {
        Lcc lcc = Make(lat1Deg, lat2Deg, lat0Deg, lon0Deg, x0, y0);
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, key, "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture) * Deg;
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture) * Deg;
            lcc.Forward(lat, lon, out double x, out double y);
            lcc.Inverse(x, y, out double latBack, out double lonBack);
            Assert.True(Math.Abs(latBack - lat) < 1e-10,
                $"lat {latBack} vs {lat}");
            Assert.True(Math.Abs(lonBack - lon) < 1e-10,
                $"lon {lonBack} vs {lon}");
        }
    }
}
