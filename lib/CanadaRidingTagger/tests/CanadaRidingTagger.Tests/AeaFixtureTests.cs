using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class AeaFixtureTests
{
    private const double a = 6378137.0;
    private const double f = 1.0 / 298.257222101;
    private static readonly double e2 = 2 * f - f * f;
    private static readonly double e = Math.Sqrt(e2);
    private static readonly double n = ThirdFlattening(a, e2);
    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;

    private static readonly string FixturesRoot = LccFixtureTests.FindFixturesRoot();

    // CRS per dataset: .tmp_fixtures/gen_fixtures.py (authoritative).
    // yt: WGS84 ellipsoid above, k0 = 1.
    private static Aea Make(double lat1Deg, double lat2Deg, double lat0Deg,
                            double lon0Deg, double x0, double y0)
    {
        return new Aea(a, n, e2, e,
            lat1Deg * Deg, lat2Deg * Deg, lat0Deg * Deg, lon0Deg * Deg,
            x0, y0);
    }

    // Third flattening n = (a - b)/(a + b), b = a * sqrt(1 - e2)
    // (PROJ init.cpp).
    private static double ThirdFlattening(double a, double e2)
    {
        double b = a * Math.Sqrt(1.0 - e2);
        return (a - b) / (a + b);
    }

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 forward projection of the same
    // WGS84 grid, meters. proj.csv rows: lat,lon,x,y.
    [Theory]
    [InlineData("yt", 61.66666666666666, 68, 59, -132.5, 500000, 500000)]
    public void ForwardMatchesPyproj(string key, double lat1Deg, double lat2Deg,
                                      double lat0Deg, double lon0Deg,
                                      double x0, double y0)
    {
        Aea aea = Make(lat1Deg, lat2Deg, lat0Deg, lon0Deg, x0, y0);
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

            aea.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    // Forward then inverse returns the input point at interior grid points
    // of the yt dataset.
    [Theory]
    [InlineData("yt", 61.66666666666666, 68, 59, -132.5, 500000, 500000)]
    public void ForwardInverseRoundTrip(string key, double lat1Deg, double lat2Deg,
                                         double lat0Deg, double lon0Deg,
                                         double x0, double y0)
    {
        Aea aea = Make(lat1Deg, lat2Deg, lat0Deg, lon0Deg, x0, y0);
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, key, "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture) * Deg;
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture) * Deg;
            aea.Forward(lat, lon, out double x, out double y);
            aea.Inverse(x, y, out double latBack, out double lonBack);
            Assert.True(Math.Abs(latBack - lat) < 1e-10,
                $"lat {latBack} vs {lat}");
            Assert.True(Math.Abs(lonBack - lon) < 1e-10,
                $"lon {lonBack} vs {lon}");
        }
    }
}
