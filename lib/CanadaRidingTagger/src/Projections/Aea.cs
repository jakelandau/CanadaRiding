namespace CanadaRidingTagger.Projections;

/// <summary>
/// Albers Equal Area (and Lambert Equal Area Conic, <c>leac</c>), ellipsoidal
/// and spherical, ported verbatim (operation order and literal arithmetic)
/// from PROJ 9.8.1 <c>aea.cpp</c> (MIT). The authalic-latitude helpers are in
/// <see cref="AuxiliaryLatitude"/> (latitudes.cpp).
/// <para>
/// Core results are in semimajor-axis units; this class applies the PROJ
/// generic finalize/prepare: forward <c>x = a * core + x0</c>, inverse
/// <c>core = (m - x0) * (1/a)</c>, and subtracts/adds the central meridian
/// longitude. The scale factor <c>k0</c> is not applied by this projection
/// (as in PROJ <c>aea.cpp</c>); the latitude of origin is folded into the
/// setup state, not applied per point.
/// </para>
/// </summary>
internal sealed class Aea : IProjection
{
    private const double Eps10 = 1.0e-10;
    private const double Tol7 = 1.0e-7;
    private const double HalfPi = 1.57079632679489661923132169163975144; // M_HALFPI

    private readonly double _a, _es, _e, _oneEs, _n3, _lam0, _x0, _y0;
    private readonly double _ra; // 1/a (init.cpp)

    // pj_aea setup state (aea.cpp:131-199).
    private readonly double _ec, _n, _c, _dd, _n2, _rho0;
    private readonly double[]? _apa;
    private readonly double _qp;
    private readonly bool _ellips;

    /// <summary>
    /// Create an Albers Equal Area projection.
    /// </summary>
    /// <param name="a">Semimajor axis (meters).</param>
    /// <param name="n">Third flattening (a-b)/(a+b).</param>
    /// <param name="es">Eccentricity squared (e^2).</param>
    /// <param name="e">Eccentricity.</param>
    /// <param name="lat1">Standard parallel 1 (radians).</param>
    /// <param name="lat2">Standard parallel 2 (radians).</param>
    /// <param name="lat0">Latitude of origin (radians).</param>
    /// <param name="lon0">Central meridian (radians).</param>
    /// <param name="x0">False easting (meters).</param>
    /// <param name="y0">False northing (meters).</param>
    public Aea(double a, double n, double es, double e,
               double lat1, double lat2, double lat0, double lon0,
               double x0, double y0)
    {
        _a = a;
        _es = es;
        _e = e;
        _oneEs = 1.0 - es;
        _n3 = n;
        _lam0 = lon0;
        _x0 = x0;
        _y0 = y0;
        _ra = 1.0 / a;

        double phi1 = lat1, phi2 = lat2;

        // pj_aea setup (ellipsoidal + spherical branches, aea.cpp:131-199).
        if (Math.Abs(phi1) > HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(lat1),
                "Invalid value for lat_1: |lat_1| should be <= 90°");
        }
        if (Math.Abs(phi2) > HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(lat2),
                "Invalid value for lat_2: |lat_2| should be <= 90°");
        }
        if (Math.Abs(phi1 + phi2) < Eps10)
        {
            throw new ArgumentException(
                "Invalid value for lat_1 and lat_2: |lat_1 + lat_2| should be > 0");
        }
        double sinphi = Math.Sin(phi1);
        _n = sinphi;
        double cosphi = Math.Cos(phi1);
        bool secant = Math.Abs(phi1 - phi2) >= Eps10;
        _ellips = _es > 0.0;
        if (_ellips)
        {
            double m1, ml1;

            _apa = AuxiliaryLatitude.AuthalicLatComputeCoeffs(_n3);
            _qp = AuxiliaryLatitude.AuthalicLatQ(1.0, _e, _oneEs);
            m1 = Ellipsoid.Msfn(sinphi, cosphi, _es);
            ml1 = AuxiliaryLatitude.AuthalicLatQ(sinphi, _e, _oneEs);
            if (secant)
            {
                double m2, ml2;

                sinphi = Math.Sin(phi2);
                cosphi = Math.Cos(phi2);
                m2 = Ellipsoid.Msfn(sinphi, cosphi, _es);
                ml2 = AuxiliaryLatitude.AuthalicLatQ(sinphi, _e, _oneEs);
                if (ml2 == ml1)
                {
                    throw new ArgumentException("Standard parallels are identical");
                }

                _n = (m1 * m1 - m2 * m2) / (ml2 - ml1);
                if (_n == 0.0)
                {
                    // Not quite, but es is very close to 1...
                    throw new ArgumentOutOfRangeException(nameof(lat1),
                        "Invalid value for eccentricity");
                }
            }
            _ec = 1.0 - 0.5 * _oneEs * Math.Log((1.0 - _e) / (1.0 + _e)) / _e;
            _c = m1 * m1 + _n * ml1;
            _dd = 1.0 / _n;
            _rho0 =
                _dd * Math.Sqrt(_c - _n *
                    AuxiliaryLatitude.AuthalicLatQ(Math.Sin(lat0), _e, _oneEs));
        }
        else
        {
            if (secant)
            {
                _n = 0.5 * (_n + Math.Sin(phi2));
            }
            _n2 = _n + _n;
            _c = cosphi * cosphi + _n2 * sinphi;
            _dd = 1.0 / _n;
            _rho0 = _dd * Math.Sqrt(_c - _n2 * Math.Sin(lat0));
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

    // aea_e_forward (aea.cpp:71-85). Input (phi, lam - lam0) in radians;
    // output (easting, northing) in semimajor-axis units.
    private (double, double) CoreForward(double phi, double lam)
    {
        double rho = _c - (_ellips
            ? _n * AuxiliaryLatitude.AuthalicLatQ(Math.Sin(phi), _e, _oneEs)
            : _n2 * Math.Sin(phi));
        if (rho < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(phi),
                "Outside projection domain");
        }
        rho = _dd * Math.Sqrt(rho);
        lam *= _n;
        double x = rho * Math.Sin(lam);
        double y = _rho0 - rho * Math.Cos(lam);
        return (x, y);
    }

    // aea_e_inverse (aea.cpp:87-129). Input (easting, northing) in
    // semimajor-axis units; output (phi, lam - lam0) in radians.
    private (double, double) CoreInverse(double x, double y)
    {
        y = _rho0 - y;
        double rho = Math.Sqrt(x * x + y * y); // hypot
        double phi, lam;
        if (rho != 0.0)
        {
            if (_n < 0.0)
            {
                rho = -rho;
                x = -x;
                y = -y;
            }
            phi = rho / _dd;
            if (_ellips)
            {
                double qs = (_c - phi * phi) / _n;
                if (Math.Abs(_ec - Math.Abs(qs)) > Tol7)
                {
                    if (Math.Abs(qs) > 2.0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(x),
                            "Outside projection domain");
                    }
                    phi = AuxiliaryLatitude.AuthalicLatInverse(
                        Math.Asin(qs / _qp), _apa!, _n3, _e, _es, _oneEs, _qp);
                    if (double.IsInfinity(phi))
                    {
                        // C: if (lp.phi == HUGE_VAL) -> error.
                        throw new ArgumentOutOfRangeException(nameof(x),
                            "Outside projection domain");
                    }
                }
                else
                {
                    phi = qs < 0.0 ? -HalfPi : HalfPi;
                }
            }
            else
            {
                double qs_div_2 = (_c - phi * phi) / _n2;
                if (Math.Abs(qs_div_2) <= 1.0)
                {
                    phi = Math.Asin(qs_div_2);
                }
                else
                {
                    phi = qs_div_2 < 0.0 ? -HalfPi : HalfPi;
                }
            }
            lam = Math.Atan2(x, y) / _n;
        }
        else
        {
            lam = 0.0;
            phi = _n > 0.0 ? HalfPi : -HalfPi;
        }
        return (phi, lam);
    }
}
