using System.Globalization;
using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class StereFixtureTests
{
    private static (double es, double e) Eccentricity(double rf)
    {
        double f = 1.0 / rf;
        double e2 = 2.0 * f - f * f;
        return (e2, Math.Sqrt(e2));
    }

    // NB's CRS: NAD_1983_CSRS New Brunswick Stereographic (EPSG:3514 family).
    private static Stere Make()
    {
        (double es, double e) = Eccentricity(298.257222101);
        double lat0 = 46.5 * Deg;
        double lon0 = -66.5 * Deg;
        // phits = 0: the CRS has no tlat_ts, so PROJ defaults phits to
        // M_HALFPI.
        return new Stere(6378137.0, es, e, 0.999912, 0.0, lat0, lon0, 2500000.0, 7500000.0);
    }

    private static readonly string FixturesRoot = LccFixtureTests.FindFixturesRoot();

    private const double Deg = Math.PI / 180.0;
    private const double ToleranceMeters = 1e-4;
    private const double TolRad = 1e-10;

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 forward projection of the same
    // WGS84 grid, meters. proj.csv rows: lat,lon,x,y.
    [Fact]
    public void ForwardMatchesPyproj()
    {
        Stere s = Make();
        double maxDx = 0, maxDy = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, "nb", "proj.csv")))
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

            s.Forward(lat * Deg, lon * Deg, out double x, out double y);
            maxDx = Math.Max(maxDx, Math.Abs(x - rx));
            maxDy = Math.Max(maxDy, Math.Abs(y - ry));
        }

        Assert.True(maxDx < ToleranceMeters, $"max |dx| = {maxDx} m");
        Assert.True(maxDy < ToleranceMeters, $"max |dy| = {maxDy} m");
    }

    // Reference: pyproj 3.8.0 / PROJ 9.8.1 inverse projection of the same
    // grid, degrees. proj.csv rows: lat,lon,x,y.
    [Fact]
    public void InverseMatchesPyproj()
    {
        Stere s = Make();
        double maxDPhi = 0, maxDLam = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(FixturesRoot, "nb", "proj.csv")))
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

            s.Inverse(rx, ry, out double phi, out double lam);
            maxDPhi = Math.Max(maxDPhi, Math.Abs(phi - lat * Deg));
            maxDLam = Math.Max(maxDLam, Math.Abs(lam - lon * Deg));
        }

        Assert.True(maxDPhi < TolRad, $"max |dphi| = {maxDPhi} rad");
        Assert.True(maxDLam < TolRad, $"max |dlam| = {maxDLam} rad");
    }

    [Fact]
    public void ForwardRoundTripInverse()
    {
        Stere s = Make();
        double lat0 = 46.5 * Deg;
        double lon0 = -66.5 * Deg;
        foreach ((double dLat, double dLon) in new[]
        {
            (0.0, 0.0), (0.5, 0.2), (-0.7, -1.3), (1.2, 2.1), (-0.4, 0.9), (0.1, -2.8)
        })
        {
            double phi = lat0 + dLat * Deg;
            double lam = lon0 + dLon * Deg;
            s.Forward(phi, lam, out double x, out double y);
            s.Inverse(x, y, out double phi2, out double lam2);

            double dPhi = Math.Abs(phi - phi2);
            double dLam = Math.Abs(lam - lam2);
            Assert.True(dPhi < TolRad, $"round-trip dphi = {dPhi} rad");
            Assert.True(dLam < TolRad, $"round-trip dlam = {dLam} rad");
        }
    }
}
