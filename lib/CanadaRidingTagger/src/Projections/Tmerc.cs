namespace CanadaRidingTagger.Projections;

/// <summary>
/// Transverse Mercator algorithm selection, mirroring PROJ's <c>TMercAlgo</c>.
/// </summary>
internal enum TmercAlgo
{
    /// <summary>Evenden/Snyder series ("approximate").</summary>
    EvendenSnyder,

    /// <summary>
    /// Poder/Engsager series ("exact"); the PROJ 9.8.1 compiled default
    /// when no <c>+algo</c> or <c>proj.ini</c> override is present.
    /// </summary>
    PoderEngsager,

    /// <summary>
    /// Automatic per-point dispatch (PROJ's <c>+algo=auto</c>): exact far
    /// from the central meridian, approximate near it.
    /// </summary>
    Auto,
}

/// <summary>
/// Transverse Mercator for an ellipsoid, ported verbatim (operation order
/// and literal arithmetic) from PROJ 9.8.1 <c>tmerc.cpp</c> (MIT).
/// Both Evenden/Snyder ("approx") and Poder/Engsager ("exact")
/// implementations are included with the AUTO dispatch thresholds; the
/// spherical implementation (ellipsoidal <c>es == 0</c>) is not needed
/// by this project's datasets and is omitted.
/// <para>
/// Core results are in radians of the ellipsoid (semimajor-axis units);
/// this class applies the PROJ generic finalize/prepare:
/// forward <c>x = a * core + x0</c>, inverse <c>core = (m - x0) * (1/a)</c>,
/// and subtracts/adds the central meridian longitude.
/// </para>
/// </summary>
internal sealed class Tmerc : IProjection
{
    // Evenden/Snyder constants (tmerc.cpp:53-60).
    private const double Fc1 = 1.0;
    private const double Fc2 = .5;
    private const double Fc3 = .16666666666666666666;
    private const double Fc4 = .08333333333333333333;
    private const double Fc5 = .05;
    private const double Fc6 = .03333333333333333333;
    private const double Fc7 = .02380952380952380952;
    private const double Fc8 = .01785714285714285714;

    private const int Order = 6; // PROJ_ETMERC_ORDER (tmerc.cpp:62)
    private const double HalfPi = 1.57079632679489661923132169163975144; // M_HALFPI
    private const double DegToRad = 0.0174532925199432957692369076834503277557310211286671;

    private readonly double _a, _es, _k0, _lam0, _x0, _y0;
    private readonly double _ra; // 1/a (init.cpp)
    private readonly TmercAlgo _algo;

    // Evenden/Snyder state (setup_approx, tmerc.cpp:235-249).
    private readonly double _esp, _ml0;
    private readonly double[] _en;

    // Poder/Engsager state (setup_exact, tmerc.cpp:452-489).
    private readonly double _qn, _zb;
    private readonly double[] _cgb = new double[Order];
    private readonly double[] _cbg = new double[Order];
    private readonly double[] _utg = new double[Order];
    private readonly double[] _gtu = new double[Order];

    /// <summary>
    /// Create a transverse Mercator.
    /// </summary>
    /// <param name="a">Semimajor axis (meters).</param>
    /// <param name="n">Third flattening.</param>
    /// <param name="es">Eccentricity squared (e^2).</param>
    /// <param name="k0">Scale factor on the central meridian.</param>
    /// <param name="phi0">Latitude of origin (radians).</param>
    /// <param name="lam0">Central meridian (radians).</param>
    /// <param name="x0">False easting (meters).</param>
    /// <param name="y0">False northing (meters).</param>
    /// <param name="algo">
    /// Algorithm; the default mirrors PROJ's compiled default (Poder/Engsager).
    /// </param>
    public Tmerc(double a, double n, double es, double k0, double phi0, double lam0,
                 double x0, double y0, TmercAlgo algo = TmercAlgo.PoderEngsager)
    {
        _a = a;
        _es = es;
        _k0 = k0;
        _lam0 = lam0;
        _x0 = x0;
        _y0 = y0;
        _algo = algo;
        _ra = 1.0 / a;

        // setup_approx (ellipsoidal branch, tmerc.cpp:235-249).
        _en = MeridianArc.En(n);
        _ml0 = MeridianArc.Mlfn(phi0, Math.Sin(phi0), Math.Cos(phi0), _en);
        _esp = es / (1.0 - es);

        // setup_exact (tmerc.cpp:452-489).
        AuxiliaryLatitude.Coeffs(n, AuxLat.Conformal, AuxLat.Geographic, _cgb, 0);
        AuxiliaryLatitude.Coeffs(n, AuxLat.Geographic, AuxLat.Conformal, _cbg, 0);
        _qn = k0 * AuxiliaryLatitude.RectifyingRadius(n);
        AuxiliaryLatitude.Coeffs(n, AuxLat.Rectifying, AuxLat.Conformal, _utg, 0);
        AuxiliaryLatitude.Coeffs(n, AuxLat.Conformal, AuxLat.Rectifying, _gtu, 0);
        double z = AuxiliaryLatitude.Convert(phi0, _cbg, 0, Order);
        _zb = -_qn * AuxiliaryLatitude.Convert(z, _gtu, 0, Order);
    }

    /// <summary>
    /// Forward: geodetic latitude/longitude (radians) to meters
    /// (easting, northing).
    /// </summary>
    public void Forward(double lat, double lon, out double x, out double y)
    {
        double lam = lon - _lam0;

        double cx, cy;
        switch (_algo)
        {
            case TmercAlgo.PoderEngsager:
                (cx, cy) = ExactForward(lat, lam);
                break;
            case TmercAlgo.Auto when Math.Abs(lam) <= 3 * DegToRad:
            case TmercAlgo.EvendenSnyder:
                (cx, cy) = ApproxForward(lat, lam);
                break;
            default:
                (cx, cy) = ExactForward(lat, lam);
                break;
        }

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
        double cx = (x - _x0) * _ra;
        double cy = (y - _y0) * _ra;

        double phi, lam;
        switch (_algo)
        {
            case TmercAlgo.PoderEngsager:
                (phi, lam) = ExactInverse(cx, cy);
                break;
            case TmercAlgo.Auto when Math.Abs(cx) <= 0.053 - 0.022 * cy * cy:
            case TmercAlgo.EvendenSnyder:
                (phi, lam) = ApproxInverse(cx, cy);
                break;
            default:
                (phi, lam) = ExactInverse(cx, cy);
                break;
        }

        lat = phi;
        lon = lam + _lam0;
    }

    // approx_e_fwd (tmerc.cpp:71-116). Input is (phi, lam - lam0) in radians;
    // output is (easting, northing) in radians of the ellipsoid.
    private (double, double) ApproxForward(double phi, double lam)
    {
        if (lam < -HalfPi || lam > HalfPi)
        {
            throw new ArgumentOutOfRangeException(nameof(lam),
                "More than 90 degrees from the central meridian");
        }

        double sinphi = Math.Sin(phi);
        double cosphi = Math.Cos(phi);
        double t = Math.Abs(cosphi) > 1e-10 ? sinphi / cosphi : 0.0;
        t *= t;
        double al = cosphi * lam;
        double als = al * al;
        al /= Math.Sqrt(1.0 - _es * sinphi * sinphi);
        double n = _esp * cosphi * cosphi;

        double x = _k0 * al *
                  (Fc1 + Fc3 * als *
                          (1.0 - t + n +
                           Fc5 * als *
                               (5.0 + t * (t - 18.0) + n * (14.0 - 58.0 * t) +
                                Fc7 * als * (61.0 + t * (t * (179.0 - t) - 479.0)))));

        double y =
            _k0 *
            (MeridianArc.Mlfn(phi, sinphi, cosphi, _en) - _ml0 +
             sinphi * al * lam * Fc2 *
                 (1.0 +
                  Fc4 * als *
                      (5.0 - t + n * (9.0 + 4.0 * n) +
                       Fc6 * als *
                           (61.0 + t * (t - 58.0) + n * (270.0 - 330 * t) +
                            Fc8 * als * (1385.0 + t * (t * (543.0 - t) - 3111.0))))));

        return (x, y);
    }

    // approx_e_inv (tmerc.cpp:160-197).
    private (double, double) ApproxInverse(double x, double y)
    {
        double phi = MeridianArc.InvMlfn(_ml0 + y / _k0, _en);
        if (Math.Abs(phi) >= HalfPi)
        {
            phi = y < 0.0 ? -HalfPi : HalfPi;
            return (phi, 0.0);
        }

        double sinphi = Math.Sin(phi);
        double cosphi = Math.Cos(phi);
        double t = Math.Abs(cosphi) > 1e-10 ? sinphi / cosphi : 0.0;
        double n = _esp * cosphi * cosphi;
        double con = 1.0 - _es * sinphi * sinphi;
        double d = x * Math.Sqrt(con) / _k0;
        con *= t;
        t *= t;
        double ds = d * d;

        phi -=
            (con * ds / (1.0 - _es)) * Fc2 *
            (1.0 -
             ds * Fc4 *
                 (5.0 + t * (3.0 - 9.0 * n) + n * (1.0 - 4 * n) -
                  ds * Fc6 *
                      (61.0 + t * (90.0 - 252.0 * n + 45.0 * t) + 46.0 * n -
                       ds * Fc8 *
                           (1385.0 + t * (3633.0 + t * (4095.0 + 1575.0 * t))))));

        double lam = d *
                 (Fc1 -
                  ds * Fc3 *
                      (1.0 + 2.0 * t + n -
                       ds * Fc5 *
                           (5.0 + t * (28.0 + 24.0 * t + 8.0 * n) + 6.0 * n -
                            ds * Fc7 *
                                (61.0 + t * (662.0 + t * (1320.0 + 720.0 * t)))))) /
                 cosphi;

        return (phi, lam);
    }

    // exact_e_fwd (tmerc.cpp:300-383).
    private (double, double) ExactForward(double phi, double lam)
    {
        double Cn = AuxiliaryLatitude.Convert(phi, _cbg, 0, Order);
        double sin_Cn = Math.Sin(Cn);
        double cos_Cn = Math.Cos(Cn);
        double sin_Ce = Math.Sin(lam);
        double cos_Ce = Math.Cos(lam);

        double cos_Cn_cos_Ce = cos_Cn * cos_Ce;
        Cn = Math.Atan2(sin_Cn, cos_Cn_cos_Ce);

        double inv_denom_tan_Ce = 1.0 / Hypot(sin_Cn, cos_Cn_cos_Ce);
        double tan_Ce = sin_Ce * cos_Cn * inv_denom_tan_Ce;

        double Ce = Math.Asinh(tan_Ce);

        double two_inv_denom_tan_Ce = 2 * inv_denom_tan_Ce;
        double two_inv_denom_tan_Ce_square =
            two_inv_denom_tan_Ce * inv_denom_tan_Ce;
        double tmp_r = cos_Cn_cos_Ce * two_inv_denom_tan_Ce_square;
        double sin_arg_r = sin_Cn * tmp_r;
        double cos_arg_r = cos_Cn_cos_Ce * tmp_r - 1;

        double sinh_arg_i = tan_Ce * two_inv_denom_tan_Ce;
        double cosh_arg_i = two_inv_denom_tan_Ce_square - 1;

        double dCn, dCe;
        Cn += ClenS(_gtu, 0, Order, sin_arg_r, cos_arg_r, sinh_arg_i,
                    cosh_arg_i, out dCn, out dCe);
        Ce += dCe;

        if (Math.Abs(Ce) > 2.623395162778)
        {
            throw new ArgumentOutOfRangeException(nameof(lam),
                "Longitude too far from the central meridian");
        }

        return (_qn * Ce, _qn * Cn + _zb); // (easting, northing)
    }

    // exact_e_inv (tmerc.cpp:386-450).
    private (double, double) ExactInverse(double x, double y)
    {
        double Cn = (y - _zb) / _qn;
        double Ce = x / _qn;

        if (Math.Abs(Ce) > 2.623395162778)
        {
            throw new ArgumentOutOfRangeException(nameof(x),
                "Easting too far from the central meridian");
        }

        double sin_arg_r = Math.Sin(2 * Cn);
        double cos_arg_r = Math.Cos(2 * Cn);

        double exp_2_Ce = Math.Exp(2 * Ce);
        double half_inv_exp_2_Ce = 0.5 / exp_2_Ce;
        double sinh_arg_i = 0.5 * exp_2_Ce - half_inv_exp_2_Ce;
        double cosh_arg_i = 0.5 * exp_2_Ce + half_inv_exp_2_Ce;

        double dCn_ignored, dCe;
        Cn += ClenS(_utg, 0, Order, sin_arg_r, cos_arg_r, sinh_arg_i,
                    cosh_arg_i, out dCn_ignored, out dCe);
        Ce += dCe;

        double sin_Cn = Math.Sin(Cn);
        double cos_Cn = Math.Cos(Cn);

        double sinhCe = Math.Sinh(Ce);
        Ce = Math.Atan2(sinhCe, cos_Cn);
        double modulus_Ce = Hypot(sinhCe, cos_Cn),
                 rr = Hypot(sin_Cn, modulus_Ce);
        Cn = Math.Atan2(sin_Cn, modulus_Ce);

        double phi = AuxiliaryLatitude.Convert(Cn, sin_Cn / rr, modulus_Ce / rr,
                                               _cgb, 0, Order);
        return (phi, Ce);
    }

    private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

    // clenS (tmerc.cpp:270-297) — the complex Clenshaw summation.
    // Returns R (real part) via the return value and both parts via out.
    private static double ClenS(double[] a, int offset, int size,
                                double sin_arg_r, double cos_arg_r,
                                double sinh_arg_i, double cosh_arg_i,
                                out double R, out double I)
    {
        double r, i;
        double hr = 0, hr1 = 0, hr2 = 0, hi = 0, hi1 = 0, hi2 = 0;

        r = 2 * cos_arg_r * cosh_arg_i;
        i = -2 * sin_arg_r * sinh_arg_i;

        hi1 = hr1 = hi = 0;
        hr = a[offset + size - 1];
        for (int p = offset + size - 2; p >= offset; p--)
        {
            hr2 = hr1;
            hi2 = hi1;
            hr1 = hr;
            hi1 = hi;
            hr = -hr2 + r * hr1 - i * hi1 + a[p];
            hi = -hi2 + i * hr1 + r * hi1;
        }

        r = sin_arg_r * cosh_arg_i;
        i = cos_arg_r * sinh_arg_i;
        R = r * hr - i * hi;
        I = r * hi + i * hr;
        return R;
    }
}
