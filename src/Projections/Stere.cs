namespace CanadaRidingTagger.Projections;

/// <summary>
/// Stereographic (Double Stereographic), ported verbatim (operation order
/// and literal arithmetic) from PROJ 9.8.1 <c>stere.cpp</c> (MIT). Both the
/// ellipsoidal and spherical branches and all modes (N_POLE, S_POLE,
/// OBLIQ, EQUIT) are included.
/// <para>
/// <c>stere.cpp</c> keeps a file-local copy <c>ssfn_</c> of the classic tsfn
/// function (the reciprocal of 9.8.1's <c>pj_tsfn</c>), so the ellipsoidal
/// oblique/equatorial paths use the local <see cref="Ssfn"/>, while the
/// polar branches call <c>pj_tsfn</c> — <see cref="Ellipsoid.Tsfn"/> in
/// 9.8.1.
/// </para>
/// <para>
/// Core results are in radians of the ellipsoid (semimajor-axis units);
/// this class applies the PROJ generic finalize/prepare:
/// forward <c>x = a * core + x0</c>, inverse <c>core = (m - x0) * (1/a)</c>,
/// and subtracts/adds the central meridian longitude. The scale factor
/// <c>k0</c> is applied inside the core, as in PROJ.
/// </para>
/// </summary>
internal sealed class Stere : IProjection
{
    private const double Eps10 = 1.0e-10;  // EPS10 (stere.cpp:26)
    private const double Tol = 1.0e-8;     // TOL (stere.cpp:27)
    private const int Niter = 8;           // NITER (stere.cpp:28)
    private const double Conv = 1.0e-10;   // CONV (stere.cpp:29)
    private const double HalfPi = 1.57079632679489661923132169163975144; // M_HALFPI
    private const double FortPi = 0.78539816339744830961566034815924554; // M_FORTPI

    // enum Mode (stere.cpp:11), anonymous namespace.
    private enum Mode
    {
        S_Pole = 0,
        N_Pole = 1,
        Obliq = 2,
        Equit = 3,
    }

    // pj_stere (stere.cpp:15-21).
    private readonly double _phits;
    private readonly double _sinX1;
    private readonly double _cosX1;
    private readonly double _akm1;
    private readonly Mode _mode;

    private readonly double _a;
    private readonly double _es;
    private readonly double _e;
    private readonly double _k0;
    private readonly double _phi0;
    private readonly double _lam0;
    private readonly double _x0;
    private readonly double _y0;
    private readonly double _ra; // 1/a (init.cpp)

    /// <param name="a">Semimajor axis (meters).</param>
    /// <param name="es">Eccentricity squared (e^2); 0 selects the spherical
    /// branch.</param>
    /// <param name="e">Eccentricity.</param>
    /// <param name="k0">Scale factor.</param>
    /// <param name="phits">
    /// True-scale parallel latitude (radians); as in PROJ, 0 (i.e. the
    /// <c>tlat_ts</c> parameter is absent) is treated as M_HALFPI (90
    /// degrees).
    /// </param>
    /// <param name="lat0">Latitude of the projection center (radians).</param>
    /// <param name="lon0">Central meridian (radians).</param>
    /// <param name="x0">False easting (meters).</param>
    /// <param name="y0">False northing (meters).</param>
    public Stere(double a, double es, double e, double k0, double phits,
                 double lat0, double lon0, double x0, double y0)
    {
        _a = a; _es = es; _e = e; _k0 = k0;
        _phi0 = lat0; _lam0 = lon0; _x0 = x0; _y0 = y0;
        _ra = 1.0 / a;

        // PJ_PROJECTION(stere) (stere.cpp:295-307): phits is M_HALFPI when
        // tlat_ts is absent, then fabs'd (stere.cpp:241).
        _phits = phits == 0.0 ? HalfPi : Math.Abs(phits);

        // stere_setup (stere.cpp:233-293).
        double t;
        if (Math.Abs((t = Math.Abs(_phi0)) - HalfPi) < Eps10)
        {
            _mode = _phi0 < 0.0 ? Mode.S_Pole : Mode.N_Pole;
        }
        else
        {
            _mode = t > Eps10 ? Mode.Obliq : Mode.Equit;
        }

        if (_es != 0.0)
        {
            double X;
            switch (_mode)
            {
                case Mode.N_Pole:
                case Mode.S_Pole:
                    if (Math.Abs(_phits - HalfPi) < Eps10)
                    {
                        _akm1 = 2.0 * _k0 /
                            Math.Sqrt(Math.Pow(1.0 + _e, 1.0 + _e) *
                                Math.Pow(1.0 - _e, 1.0 - _e));
                    }
                    else
                    {
                        t = Math.Sin(_phits);
                        _akm1 = Math.Cos(_phits) / Ellipsoid.Tsfn(_phits, t, _e);
                        t *= _e;
                        _akm1 /= Math.Sqrt(1.0 - t * t);
                    }
                    break;
                case Mode.Equit:
                case Mode.Obliq:
                    t = Math.Sin(_phi0);
                    X = 2.0 * Math.Atan(Ssfn(_phi0, t, _e)) - HalfPi;
                    t *= _e;
                    _akm1 = 2.0 * _k0 * Math.Cos(_phi0) / Math.Sqrt(1.0 - t * t);
                    _sinX1 = Math.Sin(X);
                    _cosX1 = Math.Cos(X);
                    break;
            }
        }
        else
        {
            switch (_mode)
            {
                case Mode.Obliq:
                    // sinph0/cosph0 are aliases for Q->sinX1/Q->cosX1
                    // (stere.cpp:24-25).
                    _sinX1 = Math.Sin(_phi0);
                    _cosX1 = Math.Cos(_phi0);
                    goto case Mode.Equit;
                case Mode.Equit:
                    _akm1 = 2.0 * _k0;
                    break;
                case Mode.S_Pole:
                case Mode.N_Pole:
                    _akm1 = Math.Abs(_phits - HalfPi) >= Eps10
                        ? Math.Cos(_phits) / Math.Tan(FortPi - 0.5 * _phits)
                        : 2.0 * _k0;
                    break;
            }
        }
    }

    // PROJ generic finalize (fwd.cpp:19-24): x = a * core + x0
    // (units = meters).
    public void Forward(double lat, double lon, out double x, out double y)
    {
        (double cx, double cy) = _es != 0.0
            ? EllForward(lat, lon - _lam0)
            : SphForward(lat, lon - _lam0);

        x = _a * cx + _x0;
        y = _a * cy + _y0;
    }

    // PROJ generic prepare (inv.cpp:17-23): core = (m - x0) * (1/a).
    public void Inverse(double x, double y, out double lat, out double lon)
    {
        (double phi, double lam) = _es != 0.0
            ? EllInverse((x - _x0) * _ra, (y - _y0) * _ra)
            : SphInverse((x - _x0) * _ra, (y - _y0) * _ra);

        lat = phi;
        lon = lam + _lam0;
    }

    // stere_e_forward (stere.cpp:37-92). Input is (phi, lam - lam0) in
    // radians; output is (easting, northing) in radians of the ellipsoid.
    private (double, double) EllForward(double phi, double lam)
    {
        double coslam = Math.Cos(lam);
        double sinlam = Math.Sin(lam);
        double sinphi = Math.Sin(phi);
        double sinX = 0.0;
        double cosX = 0.0;
        double A = 0.0;

        if (_mode == Mode.Obliq || _mode == Mode.Equit)
        {
            double X = 2.0 * Math.Atan(Ssfn(phi, sinphi, _e)) - HalfPi;
            sinX = Math.Sin(X);
            cosX = Math.Cos(X);
        }

        double x = 0.0;
        double y = 0.0;
        switch (_mode)
        {
            case Mode.Obliq:
                double denom =
                    _cosX1 * (1.0 + _sinX1 * sinX + _cosX1 * cosX * coslam);
                if (denom == 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(phi),
                        "Outside projection domain");
                }
                A = _akm1 / denom;
                y = A * (_cosX1 * sinX - _sinX1 * cosX * coslam);
                x = A * cosX;
                break;
            case Mode.Equit:
                // avoid zero division
                if (1.0 + cosX * coslam == 0.0)
                {
                    y = double.PositiveInfinity; // HUGE_VAL
                }
                else
                {
                    A = _akm1 / (1.0 + cosX * coslam);
                    y = A * sinX;
                }
                x = A * cosX;
                break;
            case Mode.S_Pole:
                phi = -phi;
                coslam = -coslam;
                sinphi = -sinphi;
                goto case Mode.N_Pole;
            case Mode.N_Pole:
                if (Math.Abs(phi - HalfPi) < 1e-15)
                {
                    x = 0;
                }
                else
                {
                    x = _akm1 * Ellipsoid.Tsfn(phi, sinphi, _e);
                }
                y = -x * coslam;
                break;
        }

        x = x * sinlam;
        return (x, y);
    }

    // stere_s_forward (stere.cpp:94-135). Input is (phi, lam - lam0) in
    // radians; output is (easting, northing) in radians of the sphere.
    private (double, double) SphForward(double phi, double lam)
    {
        double sinphi = Math.Sin(phi);
        double cosphi = Math.Cos(phi);
        double coslam = Math.Cos(lam);
        double sinlam = Math.Sin(lam);

        double x = 0.0;
        double y = 0.0;
        switch (_mode)
        {
            case Mode.Equit:
                y = 1.0 + cosphi * coslam;
                goto Oblcon;
            case Mode.Obliq:
                y = 1.0 + _sinX1 * sinphi + _cosX1 * cosphi * coslam;
                goto Oblcon;
            Oblcon:
                if (y <= Eps10)
                {
                    throw new ArgumentOutOfRangeException(nameof(phi),
                        "Outside projection domain");
                }
                y = _akm1 / y;
                x = y * cosphi * sinlam;
                y *= _mode == Mode.Equit
                    ? sinphi
                    : _cosX1 * sinphi - _sinX1 * cosphi * coslam;
                break;
            case Mode.N_Pole:
                coslam = -coslam;
                phi = -phi;
                goto case Mode.S_Pole;
            case Mode.S_Pole:
                if (Math.Abs(phi - HalfPi) < Tol)
                {
                    throw new ArgumentOutOfRangeException(nameof(phi),
                        "Outside projection domain");
                }
                y = _akm1 * Math.Tan(FortPi + 0.5 * phi);
                x = sinlam * y;
                y *= coslam;
                break;
        }

        return (x, y);
    }

    // stere_e_inverse (stere.cpp:137-188). Input is (easting, northing) in
    // radians of the ellipsoid; output is (phi, lam - lam0) in radians.
    private (double, double) EllInverse(double x, double y)
    {
        double rho = Hypot(x, y);
        double phi = 0.0;
        double lam = 0.0;
        double cosphi = 0.0;
        double sinphi = 0.0;
        double tp = 0.0;
        double phi_l = 0.0;
        double halfe = 0.0;
        double halfpi = 0.0;

        switch (_mode)
        {
            case Mode.Obliq:
            case Mode.Equit:
                tp = 2.0 * Math.Atan2(rho * _cosX1, _akm1);
                cosphi = Math.Cos(tp);
                sinphi = Math.Sin(tp);
                if (rho == 0.0)
                {
                    phi_l = Math.Asin(cosphi * _sinX1);
                }
                else
                {
                    phi_l = Math.Asin(cosphi * _sinX1 +
                        (y * sinphi * _cosX1 / rho));
                }

                tp = Math.Tan(0.5 * (HalfPi + phi_l));
                x *= sinphi;
                y = rho * _cosX1 * cosphi - y * _sinX1 * sinphi;
                halfpi = HalfPi;
                halfe = 0.5 * _e;
                break;
            case Mode.N_Pole:
                y = -y;
                goto case Mode.S_Pole;
            case Mode.S_Pole:
                tp = -rho / _akm1;
                phi_l = HalfPi - 2.0 * Math.Atan(tp);
                halfpi = -HalfPi;
                halfe = -0.5 * _e;
                break;
        }

        for (int i = Niter; i > 0; --i)
        {
            sinphi = _e * Math.Sin(phi_l);
            phi = 2.0 * Math.Atan(tp *
                Math.Pow((1.0 + sinphi) / (1.0 - sinphi), halfe)) - halfpi;
            if (Math.Abs(phi_l - phi) < Conv)
            {
                if (_mode == Mode.S_Pole)
                {
                    phi = -phi;
                }
                lam = (x == 0.0 && y == 0.0) ? 0.0 : Math.Atan2(x, y);
                return (phi, lam);
            }
            phi_l = phi;
        }

        throw new ArgumentOutOfRangeException(nameof(x),
            "Outside projection domain");
    }

    // stere_s_inverse (stere.cpp:190-231). Input is (easting, northing) in
    // radians of the sphere; output is (phi, lam - lam0) in radians.
    private (double, double) SphInverse(double x, double y)
    {
        double phi = 0.0;
        double lam = 0.0;
        double c;
        double rh = Hypot(x, y);

        c = 2.0 * Math.Atan(rh / _akm1);
        double sinc = Math.Sin(c);
        double cosc = Math.Cos(c);
        lam = 0.0;

        switch (_mode)
        {
            case Mode.Equit:
                if (Math.Abs(rh) <= Eps10)
                {
                    phi = 0.0;
                }
                else
                {
                    phi = Math.Asin(y * sinc / rh);
                }
                if (cosc != 0.0 || x != 0.0)
                {
                    lam = Math.Atan2(x * sinc, cosc * rh);
                }
                break;
            case Mode.Obliq:
                if (Math.Abs(rh) <= Eps10)
                {
                    phi = _phi0;
                }
                else
                {
                    phi = Math.Asin(cosc * _sinX1 + y * sinc * _cosX1 / rh);
                }
                c = cosc - _sinX1 * Math.Sin(phi);
                if (c != 0.0 || x != 0.0)
                {
                    lam = Math.Atan2(x * sinc * _cosX1, c * rh);
                }
                break;
            case Mode.N_Pole:
                y = -y;
                goto case Mode.S_Pole;
            case Mode.S_Pole:
                if (Math.Abs(rh) <= Eps10)
                {
                    phi = _phi0;
                }
                else
                {
                    phi = Math.Asin(_mode == Mode.S_Pole ? -cosc : cosc);
                }
                lam = (x == 0.0 && y == 0.0) ? 0.0 : Math.Atan2(x, y);
                break;
        }

        return (phi, lam);
    }

    // ssfn_ (stere.cpp:31-35): stere.cpp's file-local copy of the classic
    // tsfn function (reciprocal of 9.8.1's pj_tsfn = Ellipsoid.Tsfn).
    private static double Ssfn(double phit, double sinphi, double eccen)
    {
        sinphi *= eccen;
        return Math.Tan(0.5 * (HalfPi + phit)) *
               Math.Pow((1.0 - sinphi) / (1.0 + sinphi), 0.5 * eccen);
    }

    // C hypot on O(1) radians; inputs bounded by a few units.
    private static double Hypot(double x, double y)
    {
        return Math.Sqrt(x * x + y * y);
    }
}
