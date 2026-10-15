namespace CanadaRidingTagger;

/// <summary>
/// Maps a federal electoral district to its province or territory.
/// The jurisdiction is carried by the first two digits of the district's code
/// (the five-digit numbering scheme assigned by Elections Canada, in which
/// each province/territory owns a two-digit block).
/// </summary>
public static class FederalJurisdiction
{
    /// <summary>
    /// The province or territory of a federal electoral district, by the first two
    /// digits of its code.
    /// </summary>
    /// <param name="id">
    /// The federal district code. For the standard federal dataset this is the
    /// five-digit <c>FED_NUM</c> (e.g. <c>"10004"</c>, <c>"35025"</c>). Only the
    /// first two digits are consulted, so a longer or differently formatted id
    /// still resolves so long as its two-digit prefix is a recognized block.
    /// </param>
    /// <returns>
    /// The province or territory, or <c>null</c> when <paramref name="id"/> is
    /// <c>null</c>, shorter than two characters, or its prefix is not a
    /// recognized jurisdiction block.
    /// </returns>
    public static Jurisdiction? FromId(string? id)
    {
        if (id is null || id.Length < 2)
        {
            return null;
        }

        // Two-digit jurisdiction block, first two characters of the code.
        // Allocation-free: switches on characters, not a substring.
        return (id[0], id[1]) switch
        {
            ('1', '0') => Jurisdiction.NewfoundlandLabrador,
            ('1', '1') => Jurisdiction.PEI,
            ('1', '2') => Jurisdiction.NovaScotia,
            ('1', '3') => Jurisdiction.NewBrunswick,
            ('2', '4') => Jurisdiction.Quebec,
            ('3', '5') => Jurisdiction.Ontario,
            ('4', '6') => Jurisdiction.Manitoba,
            ('4', '7') => Jurisdiction.Saskatchewan,
            ('4', '8') => Jurisdiction.Alberta,
            ('5', '9') => Jurisdiction.BritishColumbia,
            ('6', '0') => Jurisdiction.Yukon,
            ('6', '1') => Jurisdiction.NorthwestTerritories,
            ('6', '2') => Jurisdiction.Nunavut,
            _ => null,
        };
    }
}
