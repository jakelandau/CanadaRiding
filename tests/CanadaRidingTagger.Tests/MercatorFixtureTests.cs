using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class MercatorFixtureTests
{
    // pei dataset CRS: EPSG:3857 (Web Mercator), sphere a = b = 6378137,
    // k = 1, lat_0 = 0, lon_0 = 0, x_0 = 0, y_0 = 0.
    private static Mercator Make()
    {
        return new Mercator(6378137.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0);
    }

    private static readonly string FixturesRoot = LccFixtureTests.FindFixturesRoot();

    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 forward projection of the same
    // WGS84 grid, meters. proj.csv rows: lat,lon,x,y.
    [Fact]
    public void ForwardMatchesPyproj()
    {
        Mercator merc = Make();
        double maxDx = 0, maxDy = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, "pei", "proj.csv")))
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

            merc.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    // Forward then inverse returns the input point at all fixture grid
    // points.
    [Fact]
    public void ForwardInverseRoundTrip()
    {
        Mercator merc = Make();
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, "pei", "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture) * Deg;
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture) * Deg;
            merc.Forward(lat, lon, out double x, out double y);
            merc.Inverse(x, y, out double latBack, out double lonBack);
            Assert.True(Math.Abs(latBack - lat) < 1e-10,
                $"lat {latBack} vs {lat}");
            Assert.True(Math.Abs(lonBack - lon) < 1e-10,
                $"lon {lonBack} vs {lon}");
        }
    }
}
