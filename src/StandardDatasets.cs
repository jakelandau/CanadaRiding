namespace CanadaRidingTagger;

/// <summary>
/// The standard Canadian electoral-district datasets: one federal dataset plus one per
/// province and territory, each carrying its attribute-field definitions.
/// Paths are rooted at a data directory that contains <c>Federal/</c>,
/// <c>Provincial/</c>, and <c>Territorial/</c>.
/// </summary>
public static class StandardDatasets
{
    private static readonly Entry[] Entries =
    [
        new("Federal/SHP/FED_CA_2025_EN.shp", new("FED_NUM", "ED_NAMEE", "ED_NAMEF"), null),
        new("Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.shp", new("EDNumber20", "EDName2017", null), Jurisdiction.Alberta),
        new("Provincial/BC/EBC_ELECTORAL_DISTS_BS11_SVW/EBC_ED_23_polygon.shp", new("ED_ID", "ED_NAME", null), Jurisdiction.BritishColumbia),
        new("Provincial/MB/VA_MB2023_public.shp", new("EDNUM", "EDNAMEEN", "EDNAMEFR"), Jurisdiction.Manitoba),
        new("Provincial/SK/ConstituencyGE30th.shp", new("Constitu_1", "Constituen", null), Jurisdiction.Saskatchewan),
        new("Provincial/ON/Electoral District Shapefile - 2022 General Election/ELECTORAL_DISTRICT.shp", new("ED_ID", "ENGLISH_NA", "FRENCH_NAM"), Jurisdiction.Ontario),
        new("Provincial/NB/Prov_PED_2024.shp", new("DIST_ID", "PED_Name_E", "PED_Name_F"), Jurisdiction.NewBrunswick),
        new("Provincial/NS/NS_2026ED_Bnds.shp", new("ED_NO", "ED_NAME", null), Jurisdiction.NovaScotia),
        new("Provincial/NL/NL_Provincial_Electoral_Districts.geojson", new("id", "dist_name", null), Jurisdiction.NewfoundlandLabrador),
        new("Territorial/NU/EN_FUTURE_NU_Constituencies.shp", new(null, "Name_E", "Name_Fr"), Jurisdiction.Nunavut),
        new("Territorial/NWT/ElectoralDistricts.shp", new(null, "ED", null), Jurisdiction.NorthwestTerritories),
        new("Territorial/YT/Yukon_Electoral_Districts.shp", new(null, "ELECT_NAME", "FR_ELECNAM"), Jurisdiction.Yukon),
        new("Provincial/PEI/Electoral_District_Boundaries.shp", new("DIST_NO", "DISTRICT", null), Jurisdiction.PEI),
        new("Provincial/QC/circonscriptions_électorales_2026_shapefile/Circonscription_electorales_2026_shapefile.shp", new("CO_CEP", "NM_CEP", null), Jurisdiction.Quebec),
    ];

    /// <summary>
    /// All 14 standard datasets, rooted at <paramref name="dataRoot"/>.
    /// </summary>
    public static IReadOnlyList<DistrictSource> All(string dataRoot) =>
        Entries.Select(e => Source(dataRoot, e)).ToList();

    /// <summary>The standard federal dataset, rooted at <paramref name="dataRoot"/>.</summary>
    public static DistrictSource Federal(string dataRoot) => Source(dataRoot, Entries[0]);

    /// <summary>The standard dataset for a province or territory, rooted at <paramref name="dataRoot"/>.</summary>
    public static DistrictSource Source(string dataRoot, Jurisdiction jurisdiction)
    {
        var entry = Entries.FirstOrDefault(e => e.Jurisdiction == jurisdiction)
            ?? throw new ArgumentException($"No standard dataset for {jurisdiction}.", nameof(jurisdiction));
        return Source(dataRoot, entry);
    }

    private static DistrictSource Source(string dataRoot, Entry entry) =>
        new(Path.Combine(dataRoot, entry.RelativePath), entry.Definition, entry.Jurisdiction);

    private sealed record Entry(string RelativePath, DatasetDefinition Definition, Jurisdiction? Jurisdiction);
}
