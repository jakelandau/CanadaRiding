# CanadaRiding

Monorepo coded in C# / .NET 10 LTS with AI assistance, featuring two core software products:

Furthermore, this README file has been revised with my fleshy human fingers, since I'm a writer by trade and it would be embarrassing not to, but using the AI-established layout so it doesn't get confused reflecting on my words when performing agentic tasks using the README as a reference.


## Layout

- `lib/CanadaRidingTagger/` — the .NET 10 class library. See its README for the
  public API, usage, and the bundled-datasets table.
- `app/FindMyRiding/` — the .NET MAUI (`net10.0-android`) app, which ships the
  datasets as Android assets and consumes the library via a project reference.
  `harness/` is a console app that loads every dataset and answers a point
  query (end-to-end smoke test); `tools/` holds various tooling and diagnostic
  scripts, and `wlsrc/`, the offline MAUI/Android workload install kit.
- `data/Shapefiles/` — the single source of truth for all 14 electoral-boundary
  datasets (`Federal/`, `Provincial/`, `Territorial/`), shared by the library,
  the app, and the harness. Pull requests seeking to merge additional data sources
  or correct flaws in the existing data sources are not only welcome, but encouraged.

## Building

```
dotnet test lib/CanadaRidingTagger/CanadaRidingTagger.sln
dotnet build app/FindMyRiding/FindMyRidingApp.csproj
dotnet pack lib/CanadaRidingTagger/CanadaRidingTagger.csproj -c Release
dotnet run --project app/FindMyRiding/harness
```

Building the MAUI app with Android as the target requires the MAUI and Android workloads,
using  (`dotnet workload install maui-android android`) or the offline kit under
`app/FindMyRiding/tools/wlsrc/`).

## Publishing an APK

Publish profile: `app/FindMyRiding/Properties/PublishProfiles/FindMyRidingApp.Sideloader.pubxml`
(Release, `android-arm64`, file-system output):

- **Visual Studio**: right-click `FindMyRidingApp` → **Publish** → select the
  **Sideloader** profile → Publish.
- **CLI** (same output):

```
dotnet publish app/FindMyRiding/FindMyRidingApp.csproj -c Release -r android-arm64 -p:PublishProfile=FindMyRidingApp.Sideloader.pubxml
```

Installable APK is located at: `app/FindMyRiding/bin/Release/net10.0-android/android-arm64/sideload/com.findmyriding.app-Signed.apk`.

It is signed with the local debug keystore — fine for sideloading, not for upload
to any Android storefront. You may receive a warning from Play Protect as the
local debug keystore is not recognized, but you can select "Install Anyway".

## Sideload

Requires an arm64 phone running Android 8.0 (API 26) or newer.

- Copy the APK to the device and tap to install
- Over USB, using ADB:
  `app/FindMyRiding/tools/platform-tools/adb install -r com.findmyriding.app-Signed.apk`

On first launch, the bundled shapefiles extract into app data (cached under a
  `.extracted` marker). Uninstall the app for a clean asset test.

## APK size

Only the files the app reads are shipped as assets: per dataset it opens
`.shp`/`.dbf`/`.shx` (+`.cpg`), so the build excludes source archives
(`.zip`), duplicate encodings (`.geojson`/`.kml`), and metadata
(`.pdf`/`.docx`/`.xml`/`.sbn`/`.sbx`) from `data/Shapefiles/` (see the
`AndroidAsset` items in the csproj).

## AOT and trimming

- Release enables AOT (`RunAOTCompilation=true` under `Release|AnyCPU`;
  Debug stays off). All 106 managed assemblies ship as
  `lib/arm64-v8a/libaot-*.dll.so` natives alongside the Mono runtime; the
  interpreted fallback (`libassembly-store.so` + dex) is kept for
  reflection safety. Measured on clean builds: AOT adds 3,537,367 B
  (~3.5 MB) over the interpreted Release APK (78,423,690 vs 81,961,057 B
  signed).
- AOT requires trimming: the build fails with XA1030 if
  `PublishTrimmed=false`, so the Release APK is always ILLink-trimmed.
  (`RunLinker=false` in the csproj is a retired no-op on .NET 10; the real
  switch is `PublishTrimmed`, auto-true here.) Interpreted (AOT-off)
  builds measured 78,423,690 B trimmed vs 98,174,602 B untrimmed —
  trimming saves 19,750,912 B (~19.75 MB) in that configuration.
- AOT + trimming can break reflection-based MAUI behavior; after changing
  either setting, test on a real device (launch, dataset extraction,
  point query).

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
