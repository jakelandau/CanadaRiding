using CanadaRidingTagger;

namespace CanadaRidingTagger.Readers;

/// <summary>
/// Builds <see cref="District"/> identities from raw attribute access.
/// Shared by the shapefile, GeoJSON, and KML readers.
/// </summary>
internal static class DistrictFactory
{
    /// <summary>
    /// Extracts one district. <paramref name="attribute"/> looks up a field name
    /// and returns its value as a string (or <c>null</c> when absent).
    /// <paramref name="placemarkName"/> is the KML <c>&lt;name&gt;</c> value when
    /// available (datasets without an id field fall back to the district name).
    /// </summary>
    public static District Extract(
        DatasetDefinition def, Func<string, string?> attribute, string? placemarkName)
    {
        string? nameEn = Clean(attribute(def.NameEn)) ?? Clean(placemarkName);
        if (nameEn is null)
        {
            throw new FormatException($"Missing district name field '{def.NameEn}'.");
        }

        string? nameFr = def.NameFr is null ? null : Clean(attribute(def.NameFr));
        string id = def.IdField is null
            ? nameEn
            : Clean(attribute(def.IdField)) ?? nameEn;

        return new District(id, nameEn, nameFr);
    }

    /// <summary>Trims and normalizes numeric strings to whole-number form.</summary>
    public static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }
        value = value.Trim();
        if (value.Length == 0)
        {
            return null;
        }
        if (double.TryParse(value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double d)
            && d == Math.Truncate(d) && double.IsFinite(d))
        {
            return ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return value;
    }
}
