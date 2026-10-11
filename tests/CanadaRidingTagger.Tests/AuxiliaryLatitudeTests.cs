using Xunit;
using CanadaRidingTagger.Projections;

namespace CanadaRidingTagger.Tests;

public class AuxiliaryLatitudeTests
{
    private const double a = 6378137.0;
    private const double f = 1.0 / 298.257222101;
    private static readonly double n = ComputeN();
    private static readonly double[] En = MeridianArc.En(n);

    private static double ComputeN()
    {
        double e2 = 2 * f - f * f;
        double s = Math.Sqrt(1 - e2);
        return (1 - s) / (1 + s);
    }

    // Reference: pyproj 3.8.0 / PROJ 9.8.1, tmerc with k=1, x0=0, y0=0,
    // lon_0=0 (central meridian): y = a * mu(phi) — meridian arc scaled by a.
    [Theory]
    [InlineData(15, 1658989.5893476687)]
    [InlineData(30, 3320113.3978450196)]
    [InlineData(45, 4984944.377857996)]
    [InlineData(60, 6654072.819367443)]
    [InlineData(75, 8326937.587172346)]
    [InlineData(85, 9443510.140574848)]
    public void MeridianArcMatchesReference(double phiDeg, double expectedY)
    {
        double phi = phiDeg * Math.PI / 180;
        double y = MeridianArc.Mlfn(phi, Math.Sin(phi), Math.Cos(phi), En) * a;
        Assert.True(Math.Abs(y - expectedY) < 1e-3, $"{y} vs {expectedY}");
    }

    [Fact]
    public void MeridianArcRoundTrip()
    {
        foreach (double phiDeg in new[] { 10.0, 33.5, 47.25, 61.125, 78.999 })
        {
            double phi = phiDeg * Math.PI / 180;
            double mu = MeridianArc.Mlfn(phi, Math.Sin(phi), Math.Cos(phi), En);
            double back = MeridianArc.InvMlfn(mu, En);
            Assert.True(Math.Abs(back - phi) < 1e-12, $"{back} vs {phi}");
        }
    }
}
