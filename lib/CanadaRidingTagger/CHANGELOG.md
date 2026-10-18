# Changelog

All notable changes to **CanadaRidingTagger** are documented here.

- Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
- Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — pre-`1.0` uses `0.x.y`

## [Unreleased]

<!-- In flight on `dev`. To cut the next version before merging to `main`:
     1. Rename this section to `[x.y.z] - YYYY-MM-DD` (date of the cut).
     2. Add a fresh, empty `## [Unreleased]` section above it for the next cycle.
     3. Tag the cut commit `v<x.y.z>` (e.g. `v0.2.0`) - MinVer derives the exact
        version from the tag; between tags, builds are a pre-release of the next
        patch. See CanadaRidingTagger.csproj for the MinVer config. -->

- Map-vintage support: `DistrictSource` carries an optional `Year` (the
  general-election year the map is or was in effect for).
  `RidingTagger.FindRidings` accepts `federalYear` / `provincialYear` and
  answers from that vintage's map — an explicit year that is not loaded
  throws `ArgumentException`; `RidingTagger.Vintages(jurisdiction)` lists
  the loaded years ascending, and `RidingTagger.CurrentYear(jurisdiction)`
  the most recent loaded year. `null` for a jurisdiction addresses the
  federal jurisdiction.
- `StandardDatasets` gains `AllVintages(dataRoot)` (all current maps plus
  the bundled past vintages), `VintagesFor(dataRoot, jurisdiction)` and
  `PastVintagesFor(dataRoot, jurisdiction)`. Three past vintages are
  bundled as KML: Nova Scotia 2021 (55 districts), Yukon 2021 (19),
  Quebec 2022 (125).
- The KML reader no longer depends on the KML namespace (any export
  namespace loads), handles placemarks whose geometry sits inside
  `GeometryCollection`/`MultiGeometry` wrappers, and registers
  `CodePagesEncodingProvider` before decoding code page 1252 so mixed-encoding
  KML also loads on the desktop .NET runtime (code pages are built in on
  Mono/Android). It also strips a UTF-8 BOM (the bundled 2018 Manitoba
  poll-division KML has one) and skips placemarks whose name field and
  `<name>` element are both empty — poll-division exports include unassigned
  areas (e.g. the 26 in the 2017 PEI KML) that would otherwise abort the
  whole load.

## [0.2.0] - 2026-10-18

- New `RidingTagger.FindRidingsByFsa(string)`: finds the federal riding and
  province/territory district containing the WGS84 centroid of a Canadian
  forward sortation area (FSA), from its first three characters. Accepts a bare
  FSA (e.g. `K1A`) or a full postal code (e.g. `K1A 0B1`); case-insensitive.
  The FSA->coordinate conversion is fully offline (bundled table, no network).
- New `FsaCentroids` lookup: WGS84 centroids for all 1,643 FSAs of the 2021
  Census Forward Sortation Area boundary file, bundled as an embedded resource
  (`data/FsaCentroids.csv`). `Get` throws `KeyNotFoundException` for an
  FSA not in the table; `TryGet` returns `false` for that case.
- The subnational jurisdiction is now determined from the federal riding's
  two-digit code prefix (the Elections Canada federal numbering scheme) instead
  of whichever loaded provincial dataset contains the point, fixing cross-jurisdiction
  bleed when a point is covered by more than one provincial dataset (e.g. a
  Labrador point also covered by a Quebec polygon).
- New `FederalJurisdiction.FromId(string)` maps a federal district id to its
  `Jurisdiction`, or `null` for an unrecognized code.

## [0.1.0] - 2026-10-12

- Initial release.
- Identifies the Canadian federal riding and the province/territory electoral
  district containing a WGS84 latitude/longitude, from bundled electoral-boundary
  shapefiles.
- Ships federal, provincial, and territorial datasets bundled in the NuGet package.
- Ground-truth tests across all datasets, including provincial coverage.
- Built on NetTopologySuite for in-memory point-in-polygon district lookups.
