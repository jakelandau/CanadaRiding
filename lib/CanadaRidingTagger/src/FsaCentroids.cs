using System.Reflection;
using System.Globalization;

namespace CanadaRidingTagger;

/// <summary>
/// WGS84 centroids of Canadian forward sortation areas (the first three
/// characters of a postal code), from a table bundled with the library.
/// </summary>
public static class FsaCentroids
{
    private const string ResourceName = "CanadaRidingTagger.FsaCentroids.csv";

    private static readonly Dictionary<string, (double Latitude, double Longitude)> Table = Load();

    /// <summary>
    /// The number of forward sortation areas in the bundled table.
    /// </summary>
    public static int Count => Table.Count;

    /// <summary>
    /// The WGS84 centroid of the forward sortation area <paramref name="fsa"/>.
    /// Accepts the three-character FSA (e.g. <c>"K1A"</c>) or a full postal code
    /// (e.g. <c>"K1A 0B1"</c>); the FSA is its first three characters.
    /// </summary>
    /// <param name="fsa">The FSA or a full postal code, case-insensitive, optional surrounding whitespace.</param>
    /// <returns>The WGS84 latitude and longitude (degrees) of the FSA centroid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fsa"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="fsa"/> is not an FSA or full Canadian postal code.</exception>
    /// <exception cref="KeyNotFoundException">The FSA is not in the bundled table.</exception>
    public static (double Latitude, double Longitude) Get(string fsa)
    {
        var key = Normalize(fsa);
        if (Table.TryGetValue(key, out var centroid))
        {
            return centroid;
        }
        throw new KeyNotFoundException($"Forward sortation area {key} is not in the bundled table.");
    }

    /// <summary>
    /// Tries to look up the WGS84 centroid of the forward sortation area
    /// <paramref name="fsa"/>; same input rules as <see cref="Get(string)"/>.
    /// </summary>
    /// <param name="fsa">The FSA or a full postal code, case-insensitive, optional surrounding whitespace.</param>
    /// <param name="latitude">On success, the WGS84 latitude in degrees.</param>
    /// <param name="longitude">On success, the WGS84 longitude in degrees.</param>
    /// <returns><c>true</c> when the FSA is in the bundled table; otherwise <c>false</c> (a well-formed FSA the table does not cover).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fsa"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="fsa"/> is not an FSA or full Canadian postal code.</exception>
    public static bool TryGet(string fsa, out double latitude, out double longitude)
    {
        var key = Normalize(fsa);
        if (Table.TryGetValue(key, out var centroid))
        {
            latitude = centroid.Latitude;
            longitude = centroid.Longitude;
            return true;
        }
        latitude = default;
        longitude = default;
        return false;
    }

    // The FSA key from an FSA or full postal code: surrounding whitespace removed,
    // the single internal space of a formatted full postal code removed, uppercased,
    // and the first three characters taken.
    private static string Normalize(string? fsa)
    {
        ArgumentNullException.ThrowIfNull(fsa);
        var s = fsa.Trim().Replace(" ", "").ToUpperInvariant();
        int n = s.Length is 3 or 6 ? 3 : throw new FormatException($"Expected a three-character FSA or full postal code, got \"{fsa}\".");
        if (s[0] is < 'A' or > 'Z' ||
            s[1] is < '0' or > '9' ||
            s[2] is < 'A' or > 'Z')
        {
            throw new FormatException($"Expected a three-character FSA or full postal code, got \"{fsa}\".");
        }
        if (s.Length is 6 &&
            (s[3] is < '0' or > '9' ||
             s[4] is < 'A' or > 'Z' ||
             s[5] is < '0' or > '9'))
        {
            throw new FormatException($"Expected a three-character FSA or full postal code, got \"{fsa}\".");
        }
        return s[..n];
    }

    private static Dictionary<string, (double Latitude, double Longitude)> Load()
    {
        var table = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        string? line = reader.ReadLine();
        if (line is null || line.Trim() != "fsa,latitude,longitude")
        {
            throw new InvalidOperationException($"Unexpected header in embedded FSA table: {line}");
        }
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }
            var fields = line.Split(',');
            if (fields.Length != 3)
            {
                throw new InvalidOperationException($"Malformed line in embedded FSA table: {line}");
            }
            table[fields[0]] = (
                double.Parse(fields[1], CultureInfo.InvariantCulture),
                double.Parse(fields[2], CultureInfo.InvariantCulture));
        }
        return table;
    }
}
