using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class TmercFixtureTests
{
    private const double a = 6378137.0;
    private const double f = 1.0 / 298.257222101;
    private static readonly double e2 = 2 * f - f * f;
    private static readonly double n = ComputeN();
    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;

    private static readonly string FixturesRoot = FindFixturesRoot();

    // CRS per dataset: .tmp_fixtures/gen_fixtures.py (authoritative).
    // All tmerc datasets: lat_0=0, y_0=0, WGS84 ellipsoid above.
    private static Tmerc Make(double k0, double lon0Deg, double x0)
    {
        return new Tmerc(a, n, e2, k0, 0, lon0Deg * Deg, x0, 0);
    }

    private static string FindFixturesRoot()
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

    private static double ComputeN()
    {
        double s = Math.Sqrt(1 - e2);
        return (1 - s) / (1 + s);
    }

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 forward projection of the same
    // WGS84 grid, meters. proj.csv rows: lat,lon,x,y.
    [Theory]
    [InlineData("ab", 0.9992, -115, 0)]
    [InlineData("mb", 0.9996, -99, 500000)]
    [InlineData("sk", 0.9996, -105, 500000)]
    [InlineData("ns", 0.9996, -63, 500000)]
    public void ForwardMatchesPyproj(string key, double k0, double lon0Deg, double x0)
    {
        Tmerc t = Make(k0, lon0Deg, x0);
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

            t.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    // AUTO dispatch exercises both the Evenden/Snyder (within 3 degrees of
    // the central meridian) and Poder/Engsager implementations.
    [Theory]
    [InlineData("ab", 0.9992, -115, 0)]
    [InlineData("mb", 0.9996, -99, 500000)]
    [InlineData("sk", 0.9996, -105, 500000)]
    [InlineData("ns", 0.9996, -63, 500000)]
    public void ForwardAutoDispatchMatchesPyproj(string key, double k0, double lon0Deg, double x0)
    {
        Tmerc t = new(a, n, e2, k0, 0, lon0Deg * Deg, x0, 0, TmercAlgo.Auto);
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

            t.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }
        Assert.True(maxDx < ToleranceMeters, $"max dx = {maxDx:R} m");
        Assert.True(maxDy < ToleranceMeters, $"max dy = {maxDy:R} m");
    }

    // Forward then inverse returns the input point; both projections are
    // checked at interior grid points of the ab dataset.
    [Fact]
    public void ForwardInverseRoundTrip()
    {
        Tmerc t = Make(0.9992, -115, 0);
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, "ab", "proj.csv")))
        {
            if (line == "lat,lon,x,y")
            {
                continue;
            }
            string[] p = line.Split(',');
            double lat = double.Parse(p[0], CultureInfo.InvariantCulture) * Deg;
            double lon = double.Parse(p[1], CultureInfo.InvariantCulture) * Deg;
            t.Forward(lat, lon, out double x, out double y);
            t.Inverse(x, y, out double latBack, out double lonBack);
            Assert.True(Math.Abs(latBack - lat) < 1e-10,
                $"lat {latBack} vs {lat}");
            Assert.True(Math.Abs(lonBack - lon) < 1e-10,
                $"lon {lonBack} vs {lon}");
        }
    }
}
