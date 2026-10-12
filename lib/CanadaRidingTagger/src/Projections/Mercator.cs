namespace CanadaRidingTagger.Projections;

/// <summary>
/// Mercator (cylindrical), ported verbatim (operation order and literal
/// arithmetic) from PROJ 9.8.1 <c>merc.cpp</c> (MIT). Both the
/// ellipsoidal and spherical branches are included.
/// <para>
/// Core results are in radians of the ellipsoid (semimajor-axis units);
/// this class applies the PROJ generic finalize/prepare:
/// forward <c>x = a * core + x0</c>, inverse <c>core = (m - x0) * (1/a)</c>,
/// and subtracts/adds the central meridian longitude.
/// </para>
/// </summary>
internal sealed class Mercator : IProjection
{
    private const double HalfPi = 1.57079632679489661923132169163975144; // M_HALFPI

    private readonly double _a, _es, _e, _k0, _lam0, _x0, _y0;
    private readonly double _ra; // 1/a (init.cpp)

    /// <summary>
    /// Create a Mercator projection.
    /// </summary>
    /// <param name="a">Semimajor axis (meters).</param>
    /// <param name="es">Eccentricity squared (e^2); 0 selects the spherical
    /// branch.</param>
    /// <param name="e">Eccentricity.</param>
    /// <param name="k0">Scale factor.</param>
    /// <param name="phits">
    /// Latitude of true scale (radians); 0 (absent) keeps <paramref name="k0"/>.
    /// As in PROJ, a nonzero value overrides <paramref name="k0"/>
    /// (<c>msfn</c> for an ellipsoid, <c>cos</c> for a sphere).
    /// </param>
    /// <param name="lam0">Central meridian (radians).</param>
    /// <param name="x0">False easting (meters).</param>
    /// <param name="y0">False northing (meters).</param>
    public Mercator(double a, double es, double e, double k0, double phits,
                    double lam0, double x0, double y0)
    {
        _a = a;
        _es = es;
        _e = e;
        _lam0 = lam0;
        _x0 = x0;
        _y0 = y0;
        _ra = 1.0 / a;

        // PJ_PROJECTION(merc) (merc.cpp:45-74).
        if (Math.Abs(phits) >= HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(phits),
                "Invalid value for lat_ts: |lat_ts| should be <= 90 degrees");
        }
        _k0 = k0;
        if (phits != 0.0)
        {
            _k0 = _es != 0.0
                ? Ellipsoid.Msfn(Math.Sin(phits), Math.Cos(phits), _es)
                : Math.Cos(phits);
        }
    }

    /// <summary>
    /// Forward: geodetic latitude/longitude (radians) to meters
    /// (easting, northing).
    /// </summary>
    public void Forward(double lat, double lon, out double x, out double y)
    {
        (double cx, double cy) = CoreForward(lat, lon - _lam0);

        // fwd.cpp finalize: x = a * core + x0 (units = meters).
        x = _a * cx + _x0;
        y = _a * cy + _y0;
    }

    /// <summary>
    /// Inverse: meters (easting, northing) to geodetic latitude/longitude
    /// (radians).
    /// </summary>
    public void Inverse(double x, double y, out double lat, out double lon)
    {
        // inv.cpp prepare: core = (m - x0) * ra, ra = 1/a.
        (double phi, double lam) = CoreInverse(
            (x - _x0) * _ra, (y - _y0) * _ra);

        lat = phi;
        lon = lam + _lam0;
    }

    // merc_e_forward / merc_s_forward (merc.cpp:13-29). Input
    // (phi, lam - lam0) in radians; output (easting, northing) in radians
    // of the ellipsoid.
    private (double, double) CoreForward(double phi, double lam)
    {
        double cx = _k0 * lam;
        double cy;
        if (_es != 0.0)
        {
            double sphi = Math.Sin(phi);
            double cphi = Math.Cos(phi);
            cy = _k0 * (Math.Asinh(sphi / cphi) - _e * Math.Atanh(_e * sphi));
        }
        else
        {
            cy = _k0 * Math.Asinh(Math.Tan(phi));
        }
        return (cx, cy);
    }

    // merc_e_inverse / merc_s_inverse (merc.cpp:31-43). Input (easting,
    // northing) in radians of the ellipsoid; output (phi, lam - lam0) in
    // radians.
    private (double, double) CoreInverse(double x, double y)
    {
        double phi;
        if (_es != 0.0)
        {
            phi = Math.Atan(Ellipsoid.SinhPsi2TanPhi(Math.Sinh(y / _k0), _e));
        }
        else
        {
            phi = Math.Atan(Math.Sinh(y / _k0));
        }
        double lam = x / _k0;
        return (phi, lam);
    }
}
