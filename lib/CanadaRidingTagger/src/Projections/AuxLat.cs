namespace CanadaRidingTagger.Projections;

/// <summary>
/// Auxiliary latitude labels, mirroring PROJ's <c>AuxLat</c> enum
/// (values are part of the pointer-table indexing).
/// </summary>
internal enum AuxLat
{
    /// <summary>Geographic latitude phi.</summary>
    Geographic,
    /// <summary>Parametric latitude beta.</summary>
    Parametric,
    /// <summary>Geocentric latitude theta.</summary>
    Geocentric,
    /// <summary>Rectifying latitude mu.</summary>
    Rectifying,
    /// <summary>Conformal latitude chi.</summary>
    Conformal,
    /// <summary>Authalic latitude xi.</summary>
    Authalic,
    /// <summary>Number of auxiliary latitudes.</summary>
    Number = 6,
    /// <summary>Order of the series expansions (= Number, accidentally).</summary>
    Order = 6,
}

/// <summary>
/// Series expansions in the third flattening <c>n</c> for converting between
/// auxiliary latitudes, plus the meridian-arc helpers.
/// Ported verbatim (operation order and literal arithmetic) from PROJ 9.8.1
/// <c>latitudes.cpp</c> and <c>mlfn.cpp</c> (MIT).
/// Array parameters carry an offset instead of C pointer arithmetic.
/// </summary>
internal static class AuxiliaryLatitude
{
    /// <summary>Order of the expansions (AuxLat::ORDER in PROJ).</summary>
    public const int Order = 6;

    /// <summary>
    /// pj_auxlat_coeffs: fill <paramref name="f"/>[offset..offset+Order-1]
    /// with the coefficients converting <paramref name="auxIn"/> (zeta) to
    /// <paramref name="auxOut"/> (eta).
    /// </summary>
    public static void Coeffs(double n, AuxLat auxIn, AuxLat auxOut, double[] f, int offset)
    {
        const int lmax = 6;
        if (!(auxIn >= 0 && auxIn < AuxLat.Number && auxOut >= 0 && auxOut < AuxLat.Number))
        {
            throw new ArgumentOutOfRangeException(nameof(auxIn), "Bad specification for auxiliary latitude");
        }

        int k = (int)AuxLat.Number * (int)auxOut + (int)auxIn;
        int o = Ptrs[k];
        if (o == Ptrs[k + 1])
        {
            throw new ArgumentOutOfRangeException(nameof(auxIn), "Unsupported conversion between auxiliary latitudes");
        }

        double d = n, n2 = n * n;
        if (auxIn <= AuxLat.Rectifying && auxOut <= AuxLat.Rectifying)
        {
            for (int l = 0; l < lmax; ++l)
            {
                int m = (lmax - l - 1) / 2; // order of polynomial in n^2
                f[offset + l] = d * Polyval(n2, CoeffTable, o, m);
                o += m + 1;
                d *= n;
            }
        }
        else
        {
            for (int l = 0; l < lmax; ++l)
            {
                int m = (lmax - l - 1); // order of polynomial in n
                f[offset + l] = d * Polyval(n, CoeffTable, o, m);
                o += m + 1;
                d *= n;
            }
        }
    }

    /// <summary>
    /// pj_polyval: evaluate sum(p[i] * x^i, i, 0, N) via Horner's method.
    /// </summary>
    public static double Polyval(double x, double[] p, int offset, int N)
    {
        double y = N < 0 ? 0 : p[offset + N];
        for (; N > 0;)
        {
            y = y * x + p[offset + --N];
        }
        return y;
    }

    /// <summary>
    /// pj_clenshaw: evaluate y = sum(F[k] * sin((2*k+2) * zeta), k, 0, K-1)
    /// by Clenshaw summation.
    /// </summary>
    public static double Clenshaw(double szeta, double czeta, double[] f, int offset, int K)
    {
        double u0 = 0, u1 = 0,
            X = 2 * (czeta - szeta) * (czeta + szeta); // 2 * cos(2*zeta)
        for (; K > 0;)
        {
            double t = X * u0 - u1 + f[offset + --K];
            u1 = u0;
            u0 = t;
        }
        return 2 * szeta * czeta * u0; // sin(2*zeta) * u0
    }

    /// <summary>
    /// pj_auxlat_convert: convert zeta to eta (scalar return).
    /// </summary>
    public static double Convert(double zeta, double[] f, int offset, int K)
    {
        return Convert(zeta, Math.Sin(zeta), Math.Cos(zeta), f, offset, K);
    }

    /// <summary>
    /// pj_auxlat_convert: convert zeta to eta; zeta's sine and cosine given.
    /// </summary>
    public static double Convert(double zeta, double szeta, double czeta, double[] f, int offset, int K)
    {
        return zeta + Clenshaw(szeta, czeta, f, offset, K);
    }

    /// <summary>
    /// pj_auxlat_convert: convert zeta to eta; zeta's sine and cosine given,
    /// eta's sine and cosine returned. High relative accuracy near the poles.
    /// </summary>
    public static void ConvertSineCosine(double szeta, double czeta, double[] f, int offset, int K, out double seta, out double ceta)
    {
        double delta = Clenshaw(szeta, czeta, f, offset, K), sdelta = Math.Sin(delta),
            cdelta = Math.Cos(delta);
        seta = szeta * cdelta + czeta * sdelta;
        ceta = czeta * cdelta - szeta * sdelta;
    }

    /// <summary>
    /// pj_rectifying_radius: rectifying radius = quarter meridian /
    /// (pi/2 * a).
    /// </summary>
    public static double RectifyingRadius(double n)
    {
        // Expansion of (quarter meridian) / ((a+b)/2 * pi/2) as series in n^2;
        // these coefficients are ( (2*k - 3)!! / (2*k)!! )^2 for k = 0..3
        double[] coeffRad = { 1, 1.0 / 4, 1.0 / 64, 1.0 / 256 };
        return Polyval(n * n, coeffRad, 0, 3) / (1 + n);
    }

    /// <summary>
    /// PROJ_AUTHALIC_SERIES_VALID(n): the authalic series are accurate for
    /// |n| &lt; 0.01.
    /// </summary>
    private static bool AuthalicSeriesValid(double n)
    {
        return Math.Abs(n) < 0.01;
    }

    /// <summary>
    /// pj_authalic_lat_q: computes coefficient q such that
    /// authalic_latitude = beta = asin(q / qp), where qp is q at phi=90deg
    /// (qp = pj_authalic_lat_q(1, P)). Snyder (3-11) and (3-12).
    /// </summary>
    public static double AuthalicLatQ(double sinphi, double e, double one_es)
    {
        const double epsilon = 1.0e-7;
        if (e >= epsilon)
        {
            double e_sinphi = e * sinphi;
            double one_minus_e_sinphi_sq = 1.0 - e_sinphi * e_sinphi;

            /* avoid zero division, fail gracefully */
            if (one_minus_e_sinphi_sq == 0.0)
            {
                return double.PositiveInfinity; // HUGE_VAL
            }

            // Snyder uses 0.5 * ln((1-e*sinphi)/(1+e*sinphi) which is
            // -atanh(e*sinphi)
            return one_es *
                   (sinphi / one_minus_e_sinphi_sq + Math.Atanh(e_sinphi) / e);
        }
        else
        {
            return 2 * sinphi;
        }
    }

    /// <summary>
    /// pj_authalic_lat_compute_coeffs: coefficients for conversions between
    /// geographic and authalic latitude. Block 0 converts authalic to
    /// geographic; if |n| &lt; 0.01 a second block (offset Order) converts
    /// geographic to authalic.
    /// </summary>
    public static double[] AuthalicLatComputeCoeffs(double n)
    {
        const int lmax = Order;
        bool valid = AuthalicSeriesValid(n);
        double[] apa = new double[lmax * (valid ? 2 : 1)];
        Coeffs(n, AuxLat.Authalic, AuxLat.Geographic, apa, 0);
        if (valid)
        {
            Coeffs(n, AuxLat.Geographic, AuxLat.Authalic, apa, lmax);
        }
        return apa;
    }

    /// <summary>
    /// pj_authalic_lat: authalic latitude from the geographic latitude.
    /// qp is q at phi=90deg (qp = pj_authalic_lat_q(1, P)).
    /// </summary>
    public static double AuthalicLat(double phi, double sinphi, double cosphi,
        double[] apa, double n, double e, double one_es, double qp)
    {
        const int lmax = Order;
        if (AuthalicSeriesValid(n))
        {
            return Convert(phi, sinphi, cosphi, apa, lmax, lmax);
        }
        else
        {
            // This result is ill-conditioned near the poles.  So don't use this if
            // the series are accurate.
            double q = AuthalicLatQ(sinphi, e, one_es);
            double ratio = q / qp;

            if (Math.Abs(ratio) > 1)
            {
                /* Rounding error. */
                ratio = ratio > 0 ? 1 : -1;
            }
            return Math.Asin(ratio);
        }
    }

    /// <summary>
    /// pj_authalic_lat_inverse: geographic latitude from beta (authalic
    /// latitude), using coefficients from AuthalicLatComputeCoeffs.
    /// qp is q at phi=90deg (qp = pj_authalic_lat_q(1, P)).
    /// </summary>
    public static double AuthalicLatInverse(double beta, double[] apa, double n,
        double e, double es, double one_es, double qp)
    {
        double phi = Convert(beta, apa, 0, Order);
        if (AuthalicSeriesValid(n))
        {
            return phi;
        }
        // If the flattening is large, solve
        //   f(phi) = qp*sin(beta)/(1-e^2) - q(phi)/(1-e^2) = 0
        // for phi, using Newton's method.
        double q = Math.Sin(beta) * qp;
        double q_div_one_minus_es = q / one_es;
        for (int i = 0; i < 10; ++i)
        {
            double sinphi = Math.Sin(phi);
            double cosphi = Math.Cos(phi);
            double one_minus_es_sin2phi = 1 - es * (sinphi * sinphi);
            // Snyder uses 0.5 * ln((1-e*sinphi)/(1+e*sinphi) which is
            // -atanh(e*sinphi)
            double dphi =
                (one_minus_es_sin2phi * one_minus_es_sin2phi) / (2 * cosphi) *
                (q_div_one_minus_es - sinphi / one_minus_es_sin2phi -
                 Math.Atanh(e * sinphi) / e);
            if (!(Math.Abs(dphi) >= 1.0e-15))
            {
                break;
            }
            phi += dphi;
        }
        return phi;
    }

    // Machine-generated coefficient bundle (Maxima, auxlat.mac), upper
    // triangular matrices concatenated. Even-conversion (phi/beta/theta/mu
    // pairs) blocks store only even n coefficients; zero elements excluded.
    private static readonly double[] CoeffTable =
    {
        // C[phi,mu]; even coeffs only
        3.0 / 2.0, -27.0 / 32.0, 269.0 / 512.0, 21.0 / 16.0, -55.0 / 32.0,
        6759.0 / 4096.0, 151.0 / 96.0, -417.0 / 128.0, 1097.0 / 512.0,
        -15543.0 / 2560.0, 8011.0 / 2560.0, 293393.0 / 61440.0,
        // C[phi,chi]
        2.0, -2.0 / 3.0, -2.0, 116.0 / 45.0, 26.0 / 45.0, -2854.0 / 675.0,
        7.0 / 3.0, -8.0 / 5.0, -227.0 / 45.0, 2704.0 / 315.0, 2323.0 / 945.0,
        56.0 / 15.0, -136.0 / 35.0, -1262.0 / 105.0, 73814.0 / 2835.0,
        4279.0 / 630.0, -332.0 / 35.0, -399572.0 / 14175.0, 4174.0 / 315.0,
        -144838.0 / 6237.0, 601676.0 / 22275.0,
        // C[phi,xi]
        4.0 / 3.0, 4.0 / 45.0, -16.0 / 35.0, -2582.0 / 14175.0,
        60136.0 / 467775.0, 28112932.0 / 212837625.0, 46.0 / 45.0,
        152.0 / 945.0, -11966.0 / 14175.0, -21016.0 / 51975.0,
        251310128.0 / 638512875.0, 3044.0 / 2835.0, 3802.0 / 14175.0,
        -94388.0 / 66825.0, -8797648.0 / 10945935.0, 6059.0 / 4725.0,
        41072.0 / 93555.0, -1472637812.0 / 638512875.0, 768272.0 / 467775.0,
        455935736.0 / 638512875.0, 4210684958.0 / 1915538625.0,
        // C[mu,phi]; even coeffs only
        -3.0 / 2.0, 9.0 / 16.0, -3.0 / 32.0, 15.0 / 16.0, -15.0 / 32.0,
        135.0 / 2048.0, -35.0 / 48.0, 105.0 / 256.0, 315.0 / 512.0,
        -189.0 / 512.0, -693.0 / 1280.0, 1001.0 / 2048.0,
        // C[mu,chi]
        1.0 / 2.0, -2.0 / 3.0, 5.0 / 16.0, 41.0 / 180.0, -127.0 / 288.0,
        7891.0 / 37800.0, 13.0 / 48.0, -3.0 / 5.0, 557.0 / 1440.0,
        281.0 / 630.0, -1983433.0 / 1935360.0, 61.0 / 240.0, -103.0 / 140.0,
        15061.0 / 26880.0, 167603.0 / 181440.0, 49561.0 / 161280.0,
        -179.0 / 168.0, 6601661.0 / 7257600.0, 34729.0 / 80640.0,
        -3418889.0 / 1995840.0, 212378941.0 / 319334400.0,
        // C[chi,phi]
        -2.0, 2.0 / 3.0, 4.0 / 3.0, -82.0 / 45.0, 32.0 / 45.0, 4642.0 / 4725.0,
        5.0 / 3.0, -16.0 / 15.0, -13.0 / 9.0, 904.0 / 315.0, -1522.0 / 945.0,
        -26.0 / 15.0, 34.0 / 21.0, 8.0 / 5.0, -12686.0 / 2835.0, 1237.0 / 630.0,
        -12.0 / 5.0, -24832.0 / 14175.0, -734.0 / 315.0, 109598.0 / 31185.0,
        444337.0 / 155925.0,
        // C[chi,mu]
        -1.0 / 2.0, 2.0 / 3.0, -37.0 / 96.0, 1.0 / 360.0, 81.0 / 512.0,
        -96199.0 / 604800.0, -1.0 / 48.0, -1.0 / 15.0, 437.0 / 1440.0,
        -46.0 / 105.0, 1118711.0 / 3870720.0, -17.0 / 480.0, 37.0 / 840.0,
        209.0 / 4480.0, -5569.0 / 90720.0, -4397.0 / 161280.0, 11.0 / 504.0,
        830251.0 / 7257600.0, -4583.0 / 161280.0, 108847.0 / 3991680.0,
        -20648693.0 / 638668800.0,
        // C[xi,phi]
        -4.0 / 3.0, -4.0 / 45.0, 88.0 / 315.0, 538.0 / 4725.0,
        20824.0 / 467775.0, -44732.0 / 2837835.0, 34.0 / 45.0, 8.0 / 105.0,
        -2482.0 / 14175.0, -37192.0 / 467775.0, -12467764.0 / 212837625.0,
        -1532.0 / 2835.0, -898.0 / 14175.0, 54968.0 / 467775.0,
        100320856.0 / 1915538625.0, 6007.0 / 14175.0, 24496.0 / 467775.0,
        -5884124.0 / 70945875.0, -23356.0 / 66825.0, -839792.0 / 19348875.0,
        570284222.0 / 1915538625.0,
    };

    // Starting point of each conversion matrix in CoeffTable; ptrs[k] for
    // k = AuxLat.Number * auxOut + auxIn. Length NUMBER^2 + 1.
    private static readonly int[] Ptrs =
    {
        0,   0,   0,   0,   12,  33,  54,  54,  54,  54,  54,  54,  54,
        54,  54,  54,  54,  54,  54, 66,  66,  66,  66,  87,  87, 108,
        108, 108, 129, 129, 129, 150, 150, 150, 150, 150, 150,
    };

    static AuxiliaryLatitude()
    {
        if (Ptrs.Length != (int)AuxLat.Number * (int)AuxLat.Number + 1 ||
            CoeffTable.Length != Ptrs[(int)AuxLat.Number * (int)AuxLat.Number])
        {
            throw new InvalidOperationException("AuxLat coefficient table mismatch");
        }
    }
}

/// <summary>
/// Meridional distance for an ellipsoid using 6th-order expansions in the
/// third flattening <c>n</c>; ported from PROJ 9.8.1 <c>mlfn.cpp</c> (MIT).
/// </summary>
internal static class MeridianArc
{
    /// <summary>
    /// pj_enfn: the en table — [0] rectifying radius, [1..6] phi to mu
    /// coefficients, [7..12] mu to phi coefficients.
    /// </summary>
    public static double[] En(double n)
    {
        int lmax = AuxiliaryLatitude.Order;
        double[] en = new double[2 * lmax + 1];
        en[0] = AuxiliaryLatitude.RectifyingRadius(n);
        AuxiliaryLatitude.Coeffs(n, AuxLat.Geographic, AuxLat.Rectifying, en, 1);
        AuxiliaryLatitude.Coeffs(n, AuxLat.Rectifying, AuxLat.Geographic, en, 1 + lmax);
        return en;
    }

    /// <summary>pj_mlfn: meridional distance from the equator, scaled by a.</summary>
    public static double Mlfn(double phi, double sphi, double cphi, double[] en)
    {
        return en[0] * AuxiliaryLatitude.Convert(phi, sphi, cphi, en, 1, AuxiliaryLatitude.Order);
    }

    /// <summary>pj_inv_mlfn: inverse of Mlfn (mu / a -> phi).</summary>
    public static double InvMlfn(double mu, double[] en)
    {
        return AuxiliaryLatitude.Convert(mu / en[0], en, 1 + AuxiliaryLatitude.Order, AuxiliaryLatitude.Order);
    }
}
