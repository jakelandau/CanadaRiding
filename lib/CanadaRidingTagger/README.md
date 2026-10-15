# CanadaRidingTagger

A .NET 10 class library that identifies the Canadian **federal riding** and the
**province/territory electoral district** containing a WGS84 latitude/longitude,
from bundled electoral-boundary shapefiles. It is a library —
call it from any program; it is not an executable.

## Public API

### `RidingTagger`
```csharp
public sealed class RidingTagger
{
    public void Load(DistrictSource source);
    public RidingResult FindRidings(double latitude, double longitude);
}
```
- `Load` reads one district dataset (shapefile or GeoJSON), indexes its geometries
  in a spatial index, and associates them with the dataset's `Jurisdiction`
  (or federal, when `null`). Call it once per dataset. Loading is cheap to build;
  the index is per-dataset.
- `FindRidings` returns the federal district and the provincial/territorial
  district that cover the point. Each is `null` when no such district is loaded
  or the point lies outside every district. The subnational `Jurisdiction` is
  determined from the federal district's code (the Elections Canada federal
  numbering scheme); when the federal district cannot determine it, the loaded
  dataset covering the point decides.

### `StandardDatasets`
Convenience accessors for the 14 standard datasets, rooted at a data directory
that contains `Federal/`, `Provincial/`, and `Territorial/`.
```csharp
public static class StandardDatasets
{
    public static IReadOnlyList<DistrictSource> All(string dataRoot);
    public static DistrictSource Federal(string dataRoot);
    public static DistrictSource Source(string dataRoot, Jurisdiction jurisdiction);
}
```

### Records / types
| Type | Shape | Meaning |
|---|---|---|
| `RidingResult` | `(Jurisdiction? Jurisdiction, District? Federal, District? Provincial)` | Result of one query. `Jurisdiction`/`Provincial` describe the subnational district; `Federal` the federal riding. |
| `District` | `(string Id, string NameEn, string? NameFr)` | One electoral district. `Id` is `null`/name-derived when the source has no id field. |
| `DatasetDefinition` | `(string? IdField, string NameEn, string? NameFr)` | Attribute-field names a dataset uses for id / English name / French name. |
| `DistrictSource` | `(string Path, DatasetDefinition Definition, Jurisdiction? Jurisdiction)` | A loadable dataset: a file path plus its field definitions and jurisdiction. |
| `Jurisdiction` | enum | The 13 subnational jurisdictions (`Alberta` … `Nunavut`). Federal is *not* a member — it is addressed separately. |
| `FederalJurisdiction` | `static Jurisdiction? FromId(string)` | Maps a federal district id to its `Jurisdiction` (the code's first two digits), or `null` when unrecognized. |

## Usage
```csharp
using CanadaRidingTagger;

string dataRoot = Path.Combine(AppContext.BaseDirectory, "Shapefiles");

var tagger = new RidingTagger();
foreach (var source in StandardDatasets.All(dataRoot))
    tagger.Load(source);

RidingResult r = tagger.FindRidings(latitude: 43.6975888422, longitude: -79.72916262);
// r.Federal     -> District "Brampton Centre" (federal riding)
// r.Provincial  -> District for the point in Ontario
// r.Jurisdiction-> Jurisdiction.Ontario
```

## Bundled datasets

14 datasets: one federal, and one for each of the 10 provinces and 3 territories.
Paths are relative to the data root (`Shapefiles/`). The federal shapefile and
the Ontario shapefile live under nested directories.

| Jurisdiction | File (relative to data root) | Id field | English name field | French name field |
|---|---|---|---|---|
| Federal | `Federal/SHP/FED_CA_2025_EN.shp` | `FED_NUM` | `ED_NAMEE` | `ED_NAMEF` |
| Alberta | `Provincial/AB/EDS_ENACTED_BILL33_15DEC2017.shp` | `EDNumber20` | `EDName2017` | — |
| British Columbia | `Provincial/BC/EBC_ELECTORAL_DISTS_BS11_SVW/EBC_ED_23_polygon.shp` | `ED_ID` | `ED_NAME` | — |
| Manitoba | `Provincial/MB/VA_MB2023_public.shp` | `EDNUM` | `EDNAMEEN` | `EDNAMEFR` |
| Saskatchewan | `Provincial/SK/ConstituencyGE30th.shp` | `Constitu_1` | `Constituen` | — |
| Ontario | `Provincial/ON/Electoral District Shapefile - 2022 General Election/ELECTORAL_DISTRICT.shp` | `ED_ID` | `ENGLISH_NA` | `FRENCH_NAM` |
| New Brunswick | `Provincial/NB/Prov_PED_2024.shp` | `DIST_ID` | `PED_Name_E` | `PED_Name_F` |
| Nova Scotia | `Provincial/NS/NS_2026ED_Bnds.shp` | `ED_NO` | `ED_NAME` | — |
| Newfoundland and Labrador | `Provincial/NL/NL_Provincial_Electoral_Districts.shp` | `id` | `dist_name` | — |
| Nunavut | `Territorial/NU/EN_FUTURE_NU_Constituencies.shp` | — | `Name_E` | `Name_Fr` |
| Northwest Territories | `Territorial/NWT/ElectoralDistricts.shp` | — | `ED` | — |
| Yukon | `Territorial/YT/Yukon_Electoral_Districts.shp` | — | `ELECT_NAME` | `FR_ELECNAM` |
| Prince Edward Island | `Provincial/PEI/Electoral_District_Boundaries.shp` | `DIST_NO` | `DISTRICT` | — |
| Quebec | `Provincial/QC/circonscriptions_électorales_2026_shapefile/Circonscription_electorales_2026_shapefile.shp` | `CO_CEP` | `NM_CEP` | — |

Notes:
- The three territories (Nunavut, Northwest Territories, Yukon) have no numeric
  id field, so their `District.Id` is derived from the English name.
- All bundled datasets are ESRI shapefiles; the reader is selected by file extension.
- The Quebec shapefile directory name contains an accented `é`
  (`circonscriptions_électorales_2026_shapefile`) — keep it as-is when copying data.

## Layout
- `src/` — library source. `RidingTagger` and the public records are top-level in
  `CanadaRidingTagger`; projection math lives in `CanadaRidingTagger.Projections`
  (internal) and format readers in `CanadaRidingTagger.Readers` (internal).
- `tests/CanadaRidingTagger.Tests/` — unit + integration tests.
- The bundled datasets live at the monorepo root, in `data/Shapefiles/`
  (see the table above); the package ships them under
  `contentFiles/any/any/Shapefiles/`.
- `tools/` — development scripts: `gen_fixtures.py` (regenerates the test
  fixture CSVs), `ridingparser.py` (legacy KML parser), and diagnostic helpers.

## Build

From the monorepo root:

```
dotnet build lib/CanadaRidingTagger/CanadaRidingTagger.csproj
dotnet test lib/CanadaRidingTagger/CanadaRidingTagger.sln
dotnet pack lib/CanadaRidingTagger/CanadaRidingTagger.csproj -c Release
```
