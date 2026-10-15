namespace CanadaRidingTagger;

/// <summary>
/// Presentation helpers for <see cref="Jurisdiction"/> values.
/// </summary>
public static class JurisdictionExtensions
{
    /// <summary>
    /// The conventional display name of a province or territory (e.g.
    /// <c>"Quebec"</c>, <c>"Newfoundland and Labrador"</c>).
    /// </summary>
    /// <param name="jurisdiction">The province or territory.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(this Jurisdiction jurisdiction)
    {
        return jurisdiction switch
        {
            Jurisdiction.Alberta => "Alberta",
            Jurisdiction.BritishColumbia => "British Columbia",
            Jurisdiction.Manitoba => "Manitoba",
            Jurisdiction.NewBrunswick => "New Brunswick",
            Jurisdiction.NewfoundlandLabrador => "Newfoundland and Labrador",
            Jurisdiction.NovaScotia => "Nova Scotia",
            Jurisdiction.Ontario => "Ontario",
            Jurisdiction.PEI => "Prince Edward Island",
            Jurisdiction.Quebec => "Québec",
            Jurisdiction.Saskatchewan => "Saskatchewan",
            Jurisdiction.Yukon => "Yukon",
            Jurisdiction.NorthwestTerritories => "Northwest Territories",
            Jurisdiction.Nunavut => "Nunavut",
            _ => jurisdiction.ToString(),
        };
    }
}
