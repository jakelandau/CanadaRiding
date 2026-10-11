namespace CanadaRidingTagger.Projections;

/// <summary>
/// A projection that converts between geodetic coordinates and the CRS's
/// native coordinates. Geodetic coordinates are always in radians; native
/// coordinates are meters for a projected CRS, and lon/lat in degrees for
/// a bare geographic CRS (created from a top-level <c>GEOGCS</c>).
/// </summary>
internal interface IProjection
{
    /// <summary>
    /// Forward: geodetic latitude/longitude (radians) to native coordinates.
    /// Projected CRS: (easting, northing) in meters; geographic CRS:
    /// (lon, lat) in degrees.
    /// </summary>
    void Forward(double lat, double lon, out double x, out double y);

    /// <summary>
    /// Inverse: native coordinates to geodetic latitude/longitude (radians).
    /// </summary>
    void Inverse(double x, double y, out double lat, out double lon);
}
