using NetTopologySuite.Geometries;

namespace CanadaRidingTagger;

/// <summary>
/// Even-odd point-in-polygon tests on raw coordinate arrays.
/// Replaces NTS <c>Geometry.Covers</c> on the query path: same result for every
/// point except one exactly on a ring vertex/edge (measure zero; two adjacent
/// districts both match there and are ambiguous either way).
/// </summary>
internal static class PointInGeometry
{
    /// <summary>
    /// True when the point <c>(x, y)</c> (longitude, latitude, degrees) is inside
    /// <paramref name="geometry"/>. Handles <see cref="Polygon"/> and
    /// <see cref="MultiPolygon"/>; anything else returns false.
    /// </summary>
    public static bool Contains(double x, double y, Geometry geometry)
    {
        var envelope = geometry.EnvelopeInternal;
        if (x < envelope.MinX || x > envelope.MaxX || y < envelope.MinY || y > envelope.MaxY)
        {
            return false;
        }

        if (geometry is Polygon polygon)
        {
            return PointInPolygon(x, y, polygon);
        }

        if (geometry is MultiPolygon multiPolygon)
        {
            foreach (var part in multiPolygon.Geometries)
            {
                if (part is Polygon partPolygon && PointInPolygon(x, y, partPolygon))
                {
                    return true;
                }
            }
            return false;
        }

        return false;
    }

    private static bool PointInPolygon(double x, double y, Polygon polygon)
    {
        bool inside = RingOddTest(x, y, polygon.Shell.Coordinates);
        if (inside)
        {
            // Even-odd: each hole flips containment.
            foreach (var hole in polygon.Holes)
            {
                if (RingOddTest(x, y, hole.Coordinates))
                {
                    inside = !inside;
                }
            }
        }
        return inside;
    }

    /// <summary>
    /// Even-odd (odd-even rule) ray cast toward +x over one ring.
    /// </summary>
    private static bool RingOddTest(double x, double y, Coordinate[] coordinates)
    {
        int n = coordinates.Length;
        if (n < 3)
        {
            return false;
        }

        bool inside = false;
        double x1 = coordinates[0].X, y1 = coordinates[0].Y;
        for (int i = 1; i < n; i++)
        {
            double x2 = coordinates[i].X, y2 = coordinates[i].Y;
            if ((y1 > y) != (y2 > y))
            {
                double t = (y - y1) / (y2 - y1);
                if (x1 + t * (x2 - x1) > x)
                {
                    inside = !inside;
                }
            }
            x1 = x2;
            y1 = y2;
        }
        return inside;
    }
}
