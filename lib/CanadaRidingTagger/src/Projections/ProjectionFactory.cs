namespace CanadaRidingTagger.Projections;

/// <summary>
/// Creates an <see cref="IProjection"/> from an ESRI <c>.prj</c> WKT string
/// (the subset written by this project's 13 datasets). A <c>PROJCS</c>
/// yields a projection between geodetic coordinates (radians) and projected
/// coordinates (meters); a top-level <c>GEOGCS</c> yields one between
/// geodetic coordinates (radians) and lon/lat in degrees (no datum
/// transform is applied: NAD83 is accepted as WGS84).
/// </summary>
internal static class ProjectionFactory
{
    private const double Deg2Rad = Math.PI / 180.0;

    /// <summary>
    /// Create the projection described by a <c>.prj</c> WKT document.
    /// </summary>
    /// <exception cref="FormatException">
    /// Unknown or missing CRS/projection/parameter names, or an unexpected
    /// unit.
    /// </exception>
    public static IProjection Create(string wkt)
    {
        WktNode root = PrjParser.Parse(wkt);
        string? rootName = root.Name ?? throw new FormatException("Missing CRS name");
        if (rootName == "GEOGCS")
        {
            CheckUnit(root, "Degree");
            return new Geographic();
        }
        if (rootName != "PROJCS")
        {
            throw new FormatException($"Unknown CRS type: {rootName}");
        }
        CheckUnit(root, "Meter");

        // SPHEROID["name", a, 1/f]: ESRI WKT gives the semimajor axis and
        // the inverse flattening.
        WktNode spheroid = FindChild(FindChild(FindChild(root, "GEOGCS"), "DATUM"),
            "SPHEROID");
        double a = spheroid.Children[1].Number
            ?? throw new FormatException("Missing semimajor axis");
        double invF = spheroid.Children[2].Number
            ?? throw new FormatException("Missing inverse flattening");
        double f = 1.0 / invF;
        double es = 2.0 * f - f * f;
        double e = Math.Sqrt(es);
        double n = 1.0 / (2.0 * invF - 1.0);

        // PARAMETER["name", value] elements.
        var p = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (WktNode child in root.Children)
        {
            if (child.Name == "PARAMETER")
            {
                string name = child.Children[0].Value
                    ?? throw new FormatException("Malformed parameter name");
                p[name] = child.Children[1].Number
                    ?? throw new FormatException("Malformed parameter value");
            }
        }

        string projName = FindChild(root, "PROJECTION").Children[0].Value
            ?? throw new FormatException("Missing projection name");

        double x0 = P("False_Easting");
        double y0 = P("False_Northing");
        double lon0 = P("Central_Meridian") * Deg2Rad;
        double k0 = P("Scale_Factor", 1.0);
        double lat0 = P("Latitude_Of_Origin") * Deg2Rad;
        double lat1 = P("Standard_Parallel_1") * Deg2Rad;
        double lat2 = P("Standard_Parallel_2") * Deg2Rad;

        return projName switch
        {
            // Esri Lambert conformal conic (ellipsoidal).
            "Lambert_Conformal_Conic" => new Lcc(a, es, e, k0, lat1, lat2, lat0,
                lon0, x0, y0),
            // Esri Transverse Mercator (default Evenden/Snyder algorithm).
            "Transverse_Mercator" => new Tmerc(a, n, es, k0, lat0, lon0, x0, y0),
            // Esri oblique stereographic: the standard parallel is the
            // projection center, so phits is absent (0).
            "Double_Stereographic" => new Stere(a, es, e, k0, 0.0, lat0, lon0,
                x0, y0),
            // Web Mercator: spherical auxiliary sphere; Auxiliary_Sphere_Type
            // is ignored (k0 = 1 and Standard_Parallel_1 = 0 make both
            // sphere types coincide).
            "Mercator_Auxiliary_Sphere" => new Mercator(a, 0.0, 0.0, k0, lat1,
                lon0, x0, y0),
            "Mercator_1SP" => new Mercator(a, es, e, k0, 0.0, lon0, x0, y0),
            "Albers" => new Aea(a, n, es, e, lat1, lat2, lat0, lon0, x0, y0),
            _ => throw new FormatException($"Unknown projection: {projName}"),
        };

        double P(string name, double def = 0.0)
        {
            return p.TryGetValue(name, out double v) ? v : def;
        }
    }

    private static void CheckUnit(WktNode root, string expected)
    {
        string unit = FindChild(root, "UNIT").Children[0].Value
            ?? throw new FormatException("Missing unit name");
        if (unit != expected)
        {
            throw new FormatException($"Unexpected unit: {unit}");
        }
    }

    private static WktNode FindChild(WktNode node, string name)
    {
        foreach (WktNode child in node.Children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }
        throw new FormatException($"Missing element: {name}");
    }

    /// <summary>
    /// A bare geographic CRS: native coordinates are already lon/lat in
    /// degrees; only the degree/radian conversion is applied.
    /// </summary>
    private sealed class Geographic : IProjection
    {
        public void Forward(double lat, double lon, out double x, out double y)
        {
            x = lon / Deg2Rad;
            y = lat / Deg2Rad;
        }

        public void Inverse(double x, double y, out double lat, out double lon)
        {
            lat = y * Deg2Rad;
            lon = x * Deg2Rad;
        }
    }
}
