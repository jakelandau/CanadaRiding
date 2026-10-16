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
