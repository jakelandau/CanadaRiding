# Third-party licenses

Every piece of external software this library depends on or ports, by source.
Data-license provenance per dataset lives in the README (step 6); software is tracked here.

## NuGet packages (runtime dependencies)

| Package | Version | License |
| --- | --- | --- |
| NetTopologySuite | 2.6.0 | BSD-3-Clause |
| NetTopologySuite.IO.GeoJSON | 4.0.0 | BSD-3-Clause |
| NetTopologySuite.IO.ShapeFile | 2.1.0 | LGPL-2.1-or-later |

Verified 2026-10-10 from nuspec license metadata in the local NuGet cache.

Note: `NetTopologySuite.IO.ShapeFile` 2.1.0 declares **LGPL-2.1-or-later** (the other two are BSD-3-Clause). A statically linked consumer of this library may take on LGPL obligations (replacement-object-files offer); a dynamically linked consumer is unaffected.

## Ported code

| Source | Version | License | Ported content |
| --- | --- | --- | --- |
| PROJ | 9.8.1 | MIT | C# ports of the C projection cores (PROJ is a C codebase; the `.cpp` files are C compiled as C++): `lcc` (Lambert_Conformal_Conic), `tmerc` (Transverse_Mercator), `sterea` oblique stereographic ("Double_Stereographic"), `merc` (Mercator_Auxiliary_Sphere), `aea` (Albers), plus their helpers `ell_set`, `auxlat` (latitudes), `msfn`, `tsfn`, `phi2`, `clenshaw`. MIT copyright notices must be retained on the ported C# files. |

PROJ 9.5.1 sources were also extracted for cross-checking (same MIT terms). All extractions live in `.tmp_projsrc/` (working directory, not shipped).

## Dev-only tools (not shipped, no license obligations)

Python + `pyshp`, `pyproj`, `shapely` — used for data validation and ground-truth cross-checks during development; `tools/*.py` throwaway scripts.

## Data licenses (per dataset, not software)

Each dataset under `Shapefiles/` carries its own data license from its source zip/page; per-dataset provenance (source URL, license, vintage status) is recorded in the README. Examples already known: BC BCGW order zip (`licence.txt` in zip), YT `OGL-Yukon-2.0`, PEI AGO layer (2017 order), NU Elections Nunavut (OGL).
