# Third-party licenses

Every piece of external software this app depends on, by source.
Data-license provenance per dataset lives in the README; software is tracked here.

## NuGet packages (runtime dependencies)

| Package | Version | License |
| --- | --- | --- |
| Microsoft.Maui.Controls | 10.0.110 | MIT |
| CommunityToolkit.Maui | 15.0.1 | MIT |

Verified 2026-10-18 from nuspec license metadata in the local NuGet cache.

The .NET 10 runtime and Android workload that the APK embeds come from the .NET SDK
install (Microsoft, MIT).

## Via `CanadaRidingTagger` (project reference)

The app statically references `lib/CanadaRidingTagger`, so the packaged app also ships:

| Source | Version | License |
| --- | --- | --- |
| NetTopologySuite | 2.6.0 | BSD-3-Clause |
| NetTopologySuite.IO.GeoJSON | 4.0.0 | BSD-3-Clause |
| NetTopologySuite.IO.ShapeFile | 2.1.0 | LGPL-2.1-or-later |
| PROJ (ported C# projection cores) | 9.8.1 | MIT |

Full details — including the LGPL-2.1-or-later note on `NetTopologySuite.IO.ShapeFile`
and the MIT notice-retention requirement on the ported PROJ code — live in
`lib/CanadaRidingTagger/THIRD_PARTY.md`.

## Shipped data (per dataset, not software)

- `data/Shapefiles/**` — federal and provincial/territorial electoral-boundary
  shapefiles, packaged as Android assets; each dataset keeps its source government's
  data license (provenance: README).
- `data/FsaCentroids.csv` — embedded in the library; 1,637 of 1,643 rows verbatim
  from the MIT-licenced geoview-api-geolocator FSA service, remainder from
  Statistics Canada (Open Licence). Full provenance:
  `lib/CanadaRidingTagger/THIRD_PARTY.md`.

## Dev-only tools (not shipped, no license obligations)

- `tools/wlsrc/` — sources vendored from [dotnet/sdk](https://github.com/dotnet/sdk)
  v10.0.100 (MIT, .NET Foundation): the workload resolver/installer sources plus
  GitHub API dumps (`cli.json`, `tree.json`, `manifests.json`, `workloads.json`)
  used to inspect the .NET MAUI Android workload during development.
  The app's csproj excludes `tools/**` from compilation.
- PowerShell scripts under `tools/` (`*.ps1`).
