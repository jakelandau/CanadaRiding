# CanadaRiding

Monorepo for the Canadian electoral-riding identification library and the
MAUI app that demonstrates it.

## Layout

- `lib/CanadaRidingTagger/` — the .NET 10 class library. See its README for the
  public API, usage, and the bundled-datasets table.
- `app/FindMyRiding/` — the .NET MAUI (`net10.0-android`) app, which ships the
  datasets as Android assets and consumes the library via a project reference.
  `harness/` is a console app that loads every dataset and answers a point
  query (end-to-end smoke test); `tools/` holds the Android tooling — adb,
  diagnostic scripts, and `wlsrc/`, the offline MAUI/Android workload install
  kit.
- `data/Shapefiles/` — the single source of truth for all 14 electoral-boundary
  datasets (`Federal/`, `Provincial/`, `Territorial/`), shared by the library,
  the app, and the harness.

## Building

```
dotnet test lib/CanadaRidingTagger/CanadaRidingTagger.sln
dotnet build app/FindMyRiding/FindMyRidingApp.csproj
dotnet pack lib/CanadaRidingTagger/CanadaRidingTagger.csproj -c Release
dotnet run --project app/FindMyRiding/harness
```

The app build requires the MAUI and Android workloads
(`dotnet workload install maui-android android` — or the offline kit under
`app/FindMyRiding/tools/wlsrc/`).

## Data

- `data/Shapefiles/` is the only copy of the datasets; everything else reads
  from it.
- Large binary files (shapefiles, KMLs, zips, docs) are tracked with **Git LFS**;
  run `git lfs install` after cloning.
- The Quebec directory name contains an accented `é`
  (`circonscriptions_électorales_2026_shapefile`). AAPT2 cannot open it on
  Windows, so the app links the QC shapefiles under an ASCII directory name and
  renames them back at runtime (see `app/FindMyRiding/RidingData.cs`). Keep the
  real directory name as-is.
