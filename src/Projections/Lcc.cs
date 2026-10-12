namespace CanadaRidingTagger.Projections;

/// <summary>
/// Lambert conformal conic, ellipsoidal, ported verbatim (operation order
/// and literal arithmetic) from PROJ 9.8.1 <c>lcc.cpp</c> (MIT).
/// The spherical branch (<c>es == 0</c>) is not needed by this project's
/// datasets (all WGS84) and is omitted, though the <c>es != 0</c> test is
/// kept in the core.
/// <para>
/// Core results are in radians of the ellipsoid (semimajor-axis units);
/// this class applies the PROJ generic finalize/prepare:
/// forward <c>x = a * core + x0</c>, inverse <c>core = (m - x0) * (1/a)</c>,
/// and subtracts/adds the central meridian longitude. The scale factor
/// <c>k0</c> is applied inside the core, as in PROJ.
/// </para>
/// </summary>
internal sealed class Lcc : IProjection
{
    private const double Eps10 = 1.0e-10;
    private const double HalfPi = 1.57079632679489661923132169163975144; // M_HALFPI
    private const double FortPi = 0.78539816339744830961566034815924554; // M_FORTPI

    private readonly double _a, _es, _e, _k0, _lam0, _x0, _y0;
    private readonly double _ra; // 1/a (init.cpp)

    // pj_lcc setup state (lcc.cpp:118-146).
    private readonly double _n, _rho0, _c;

    /// <summary>
    /// Create a Lambert conformal conic projection.
    /// </summary>
    /// <param name="a">Semimajor axis (meters).</param>
    /// <param name="es">Eccentricity squared (e^2).</param>
    /// <param name="e">Eccentricity.</param>
    /// <param name="k0">Scale factor.</param>
    /// <param name="lat1">Standard parallel 1 (radians).</param>
    /// <param name="lat2">Standard parallel 2 (radians).</param>
    /// <param name="lat0">Latitude of origin (radians).</param>
    /// <param name="lon0">Central meridian (radians).</param>
    /// <param name="x0">False easting (meters).</param>
    /// <param name="y0">False northing (meters).</param>
    public Lcc(double a, double es, double e, double k0,
               double lat1, double lat2, double lat0, double lon0,
               double x0, double y0)
    {
        _a = a;
        _es = es;
        _e = e;
        _k0 = k0;
        _lam0 = lon0;
        _x0 = x0;
        _y0 = y0;
        _ra = 1.0 / a;

        // pj_lcc setup (ellipsoidal branch, lcc.cpp:97-146).
        if (Math.Abs(lat1 + lat2) < Eps10)
        {
            throw new ArgumentException(
                "Standard parallels must be in different hemispheres");
        }

        double sinphi = Math.Sin(lat1);
        double cosphi = Math.Cos(lat1);
        if (Math.Abs(cosphi) < Eps10 || Math.Abs(lat1) >= HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(lat1),
                "Latitude of standard parallel 1 out of domain");
        }
        if (Math.Abs(Math.Cos(lat2)) < Eps10 || Math.Abs(lat2) >= HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(lat2),
                "Latitude of standard parallel 2 out of domain");
        }

        bool secant = Math.Abs(lat1 - lat2) >= Eps10;
        double m1 = Ellipsoid.Msfn(sinphi, cosphi, _es);
        double ml1 = Ellipsoid.Tsfn(lat1, sinphi, _e);
        _n = sinphi;
        if (secant)
        {
            sinphi = Math.Sin(lat2);
            _n = Math.Log(m1 / Ellipsoid.Msfn(sinphi, Math.Cos(lat2), _es));
            if (_n == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(lat1),
                    "Standard parallels are identical");
            }
            double ml2 = Ellipsoid.Tsfn(lat2, sinphi, _e);
            double denom = Math.Log(ml1 / ml2);
            if (denom == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(lat1),
                    "Standard parallels are identical");
            }
            _n /= denom;
        }
        _rho0 = m1 * Math.Pow(ml1, -_n) / _n;
        _c = _rho0;
        _rho0 *= (Math.Abs(Math.Abs(lat0) - HalfPi) < Eps10)
            ? 0.0
            : Math.Pow(Ellipsoid.Tsfn(lat0, Math.Sin(lat0), _e), _n);
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

    // lcc_e_forward (lcc.cpp:20-38). Input (phi, lam - lam0) in radians;
    // output (easting, northing) in radians of the ellipsoid with k0
    // applied.
    private (double, double) CoreForward(double phi, double lam)
    {
        double rho;
        if (Math.Abs(Math.Abs(phi) - HalfPi) < Eps10)
        {
            if ((phi * _n) <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(phi),
                    "Outside projection domain");
            }
            rho = 0.0;
        }
        else
        {
            rho = _c * (_es != 0.0
                ? Math.Pow(Ellipsoid.Tsfn(phi, Math.Sin(phi), _e), _n)
                : Math.Pow(Math.Tan(FortPi + 0.5 * phi), -_n));
        }
        double nlam = lam * _n;
        double x = _k0 * (rho * Math.Sin(nlam));
        double y = _k0 * (_rho0 - rho * Math.Cos(nlam));
        return (x, y);
    }

    // lcc_e_inverse (lcc.cpp:40-67). Input (easting, northing) in radians
    // of the ellipsoid; output (phi, lam - lam0) in radians.
    private (double, double) CoreInverse(double x, double y)
    {
        double ex = x / _k0;
        double ey = _rho0 - y / _k0;
        double rho = Math.Sqrt(ex * ex + ey * ey);
        double phi, lam;
        if (rho != 0.0)
        {
            if (_n < 0.0)
            {
                rho = -rho;
                ex = -ex;
                ey = -ey;
            }
            phi = _es != 0.0
                ? Ellipsoid.Phi2(Math.Pow(rho / _c, 1.0 / _n), _e)
                : 2.0 * Math.Atan(Math.Pow(_c / rho, 1.0 / _n)) - HalfPi;
            // C: if (lp.phi == HUGE_VAL) -> error; effectively unreachable
            // via atan.
            if (double.IsPositiveInfinity(phi) || double.IsNegativeInfinity(phi))
            {
                throw new ArgumentOutOfRangeException(nameof(x),
                    "Outside projection domain");
            }
            lam = Math.Atan2(ex, ey) / _n;
        }
        else
        {
            lam = 0.0;
            phi = _n > 0.0 ? HalfPi : -HalfPi;
        }
        return (phi, lam);
    }
}
