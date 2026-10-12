namespace CanadaRidingTagger;

/// <summary>
/// A Canadian province or territory (the 13 subnational jurisdictions).
/// The federal jurisdiction is not a member; it is addressed separately on <see cref="RidingResult"/>.
/// </summary>
public enum Jurisdiction
{
    /// <summary>Alberta (AB).</summary>
    Alberta,
    /// <summary>British Columbia (BC).</summary>
    BritishColumbia,
    /// <summary>Manitoba (MB).</summary>
    Manitoba,
    /// <summary>New Brunswick (NB).</summary>
    NewBrunswick,
    /// <summary>Newfoundland and Labrador (NL).</summary>
    NewfoundlandLabrador,
    /// <summary>Nova Scotia (NS).</summary>
    NovaScotia,
    /// <summary>Ontario (ON).</summary>
    Ontario,
    /// <summary>Prince Edward Island (PE).</summary>
    PEI,
    /// <summary>Quebec (QC).</summary>
    Quebec,
    /// <summary>Saskatchewan (SK).</summary>
    Saskatchewan,
    /// <summary>Yukon (YT).</summary>
    Yukon,
    /// <summary>Northwest Territories (NT).</summary>
    NorthwestTerritories,
    /// <summary>Nunavut (NU).</summary>
    Nunavut,
}
