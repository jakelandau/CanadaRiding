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

## [0.1.0] - 2026-10-12

- Initial release.
- Identifies the Canadian federal riding and the province/territory electoral
  district containing a WGS84 latitude/longitude, from bundled electoral-boundary
  shapefiles.
- Ships federal, provincial, and territorial datasets bundled in the NuGet package.
- Ground-truth tests across all datasets, including provincial coverage.
- Built on NetTopologySuite for in-memory point-in-polygon district lookups.
