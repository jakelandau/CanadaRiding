namespace CanadaRidingTagger;

/// <summary>
/// The standard Canadian electoral-district datasets: one current federal dataset plus
/// one current dataset per province and territory, each carrying its attribute-field
/// definitions and the general-election year for which the map is in effect. A handful
/// of past vintages are bundled alongside the current maps.
/// Paths are rooted at a data directory that contains <c>Federal/</c>,
/// <c>Provincial/</c>, and <c>Territorial/</c>.
/// </summary>
public static class StandardDatasets
{
    private static readonly Entry[] Entries =
    [
        new("Federal/SHP/FED_CA_2025_EN.shp", new("FED_NUM", "ED_NAMEE", "ED_NAMEF"), null, 2025),
        new("Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.shp", new("EDNumber20", "EDName2017", null), Jurisdiction.Alberta, 2019),
        new("Provincial/BC/EBC_ELECTORAL_DISTS_BS11_SVW/EBC_ED_23_polygon.shp", new("ED_ID", "ED_NAME", null), Jurisdiction.BritishColumbia, 2024),
        new("Provincial/MB/VA_MB2023_public.shp", new("EDNUM", "EDNAMEEN", "EDNAMEFR"), Jurisdiction.Manitoba, 2023),
        new("Provincial/SK/ConstituencyGE30th.shp", new("Constitu_1", "Constituen", null), Jurisdiction.Saskatchewan, 2024),
        new("Provincial/ON/Electoral District Shapefile - 2022 General Election/ELECTORAL_DISTRICT.shp", new("ED_ID", "ENGLISH_NA", "FRENCH_NAM"), Jurisdiction.Ontario, 2022),
        new("Provincial/NB/Prov_PED_2024.shp", new("DIST_ID", "PED_Name_E", "PED_Name_F"), Jurisdiction.NewBrunswick, 2024),
        new("Provincial/NS/NS_2026ED_Bnds.shp", new("ED_NO", "ED_NAME", null), Jurisdiction.NovaScotia, 2026),
        new("Provincial/NL/NL_Provincial_Electoral_Districts.shp", new("id", "dist_name", null), Jurisdiction.NewfoundlandLabrador, 2021),
        new("Territorial/NU/EN_FUTURE_NU_Constituencies.shp", new(null, "Name_E", "Name_Fr"), Jurisdiction.Nunavut, 2025),
        new("Territorial/NWT/ElectoralDistricts.shp", new(null, "ED", null), Jurisdiction.NorthwestTerritories, 2023),
        new("Territorial/YT/Yukon_Electoral_Districts.shp", new(null, "ELECT_NAME", "FR_ELECNAM"), Jurisdiction.Yukon, 2025),
        new("Provincial/PEI/Electoral_District_Boundaries.shp", new("DIST_NO", "DISTRICT", null), Jurisdiction.PEI, 2023),
        new("Provincial/QC/circonscriptions_électorales_2026_shapefile/Circonscription_electorales_2026_shapefile.shp", new("CO_CEP", "NM_CEP", null), Jurisdiction.Quebec, 2026),
    ];

    // Past vintages bundled with the standard data: earlier distributions for
    // jurisdictions whose current map is a recent redistribution.
    private static readonly Entry[] PastVintages =
    [
        new("2019NovaScotia.kml", new("ED_NO", "ED_NAME", null), Jurisdiction.NovaScotia, 2021),
        new("2021Yukon.kml", new(null, "ELECT_NAME", "FR_ELECNAM"), Jurisdiction.Yukon, 2021),
        new("2022Quebec.kml", new(null, "NM_CEP", null), Jurisdiction.Quebec, 2022),
    ];

    /// <summary>
    /// All 14 current standard datasets, rooted at <paramref name="dataRoot"/>.
    /// </summary>
    public static IReadOnlyList<DistrictSource> All(string dataRoot) =>
        Entries.Select(e => Source(dataRoot, e)).ToList();

    /// <summary>
    /// All standard datasets, current maps and past vintages, rooted at
    /// <paramref name="dataRoot"/>.
    /// </summary>
    public static IReadOnlyList<DistrictSource> AllVintages(string dataRoot) =>
        Entries.Concat(PastVintages).Select(e => Source(dataRoot, e)).ToList();

    /// <summary>The standard federal dataset, rooted at <paramref name="dataRoot"/>.</summary>
    public static DistrictSource Federal(string dataRoot) => Source(dataRoot, Entries[0]);

    /// <summary>The standard dataset for a province or territory, rooted at <paramref name="dataRoot"/>.</summary>
    public static DistrictSource Source(string dataRoot, Jurisdiction jurisdiction)
    {
        var entry = Entries.FirstOrDefault(e => e.Jurisdiction == jurisdiction)
            ?? throw new ArgumentException($"No standard dataset for {jurisdiction}.", nameof(jurisdiction));
        return Source(dataRoot, entry);
    }

    /// <summary>
    /// The datasets for <paramref name="jurisdiction"/> across all available vintages,
    /// in ascending order of year, rooted at <paramref name="dataRoot"/>.
    /// </summary>
    public static IReadOnlyList<DistrictSource> VintagesFor(string dataRoot, Jurisdiction jurisdiction) =>
        AllVintages(dataRoot)
            .Where(source => source.Jurisdiction == jurisdiction)
            .OrderBy(source => source.Year)
            .ToList();

    /// <summary>
    /// The past datasets for <paramref name="jurisdiction"/>, in ascending order of
    /// year, rooted at <paramref name="dataRoot"/>. Empty for jurisdictions whose
    /// bundled standard data carries no past vintage.
    /// </summary>
    public static IReadOnlyList<DistrictSource> PastVintagesFor(string dataRoot, Jurisdiction jurisdiction) =>
        PastVintages.Where(e => e.Jurisdiction == jurisdiction)
            .OrderBy(e => e.Year)
            .Select(e => Source(dataRoot, e))
            .ToList();

    private static DistrictSource Source(string dataRoot, Entry entry) =>
        new(Path.Combine(dataRoot, entry.RelativePath), entry.Definition, entry.Jurisdiction, entry.Year);

    private sealed record Entry(string RelativePath, DatasetDefinition Definition, Jurisdiction? Jurisdiction, int Year);
}
