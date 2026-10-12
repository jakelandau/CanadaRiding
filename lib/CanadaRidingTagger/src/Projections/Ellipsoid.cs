namespace CanadaRidingTagger.Projections;

/// <summary>
/// Ellipsoid functions ported verbatim (operation order and literal
/// arithmetic) from PROJ 9.8.1 (MIT): <c>tsfn</c> (tsfn.cpp),
/// <c>msfn</c> (msfn.cpp), and <c>phi2</c> (phi2.cpp, including
/// <c>sinhpsi2tanphi</c>).
/// </summary>
internal static class Ellipsoid
{
    // pj_sinhpsi2tanphi constants (phi2.cpp:93-98).
    private const int NumIt = 5;
    private static readonly double RootEps = Math.Sqrt(double.Epsilon);
    private static readonly double Tol = RootEps / 10.0;
    private static readonly double Tmax = 2.0 / RootEps;

    /// <summary>
    /// <c>pj_tsfn</c>: small t, exp(-psi) where psi is the isometric
    /// latitude (dimensionless). tsfn.cpp:27-28.
    /// </summary>
    /// <param name="phi">Geodetic latitude (radians).</param>
    /// <param name="sinphi">sin(phi).</param>
    /// <param name="e">Eccentricity of the ellipsoid (dimensionless).</param>
    public static double Tsfn(double phi, double sinphi, double e)
    {
        double cosphi = Math.Cos(phi);
        return Math.Exp(e * Math.Atanh(e * sinphi)) *
               (sinphi > 0 ? cosphi / (1.0 + sinphi) : (1.0 - sinphi) / cosphi);
    }

    /// <summary>
    /// <c>pj_msfn</c>: meridian scale factor m = cos(phi) /
    /// sqrt(1 - es*sin^2(phi)) (dimensionless). msfn.cpp:5-6.
    /// </summary>
    /// <param name="sinphi">sin(phi).</param>
    /// <param name="cosphi">cos(phi).</param>
    /// <param name="es">Eccentricity squared (e^2).</param>
    public static double Msfn(double sinphi, double cosphi, double es)
    {
        return cosphi / Math.Sqrt(1.0 - es * sinphi * sinphi);
    }

    /// <summary>
    /// <c>pj_phi2</c>: inverse of <see cref="Tsfn"/> (phi2.cpp:124-147).
    /// </summary>
    /// <param name="ts0">Small t.</param>
    /// <param name="e">Eccentricity of the ellipsoid (dimensionless).</param>
    /// <returns>Geodetic latitude (radians).</returns>
    public static double Phi2(double ts0, double e)
    {
        return Math.Atan(SinhPsi2TanPhi((1.0 / ts0 - ts0) / 2.0, e));
    }

    /// <summary>
    /// <c>pj_sinhpsi2tanphi</c> (phi2.cpp:10-121): converts tau' =
    /// sinh(psi) to tau = tan(phi).
    /// </summary>
    public static double SinhPsi2TanPhi(double taup, double e)
    {
        double e2m = 1.0 - e * e;
        double stol = Tol * Math.Max(1.0, Math.Abs(taup));
        double tau = Math.Abs(taup) > 70 ? taup * Math.Exp(e * Math.Atanh(e)) : taup / e2m;
        if (!(Math.Abs(tau) < Tmax))
        {
            return tau;
        }
        for (int i = NumIt; i > 0; --i)
        {
            double tau1 = Math.Sqrt(1.0 + tau * tau);
            double sig = Math.Sinh(e * Math.Atanh(e * tau / tau1));
            double taupa = Math.Sqrt(1.0 + sig * sig) * tau - sig * tau1;
            double dtau = ((taup - taupa) * (1.0 + e2m * (tau * tau)) /
                           (e2m * tau1 * Math.Sqrt(1.0 + taupa * taupa)));
            tau += dtau;
            if (!(Math.Abs(dtau) >= stol))
            {
                break;
            }
        }
        return tau;
    }
}
