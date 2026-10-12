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

## Publishing an APK

Publish profile: `app/FindMyRiding/Properties/PublishProfiles/FindMyRidingApp.Sideloader.pubxml`
(Release, `android-arm64`, file-system output):

- **Visual Studio**: right-click `FindMyRidingApp` → **Publish** → select the
  **Sideloader** profile → Publish.
- **CLI** (same output):

```
dotnet publish app/FindMyRiding/FindMyRidingApp.csproj -c Release -r android-arm64
```

Installable APK: `app/FindMyRiding/bin/Release/net10.0-android/android-arm64/sideload/com.findmyriding.app-Signed.apk`.
It is signed with the local debug keystore — fine for sideloading, not for a
store.

## Sideload

Requires an arm64 phone running Android 8.0 (API 26) or newer.

- Copy the APK to the device and tap to install, or over USB:
  `app/FindMyRiding/tools/platform-tools/adb install -r com.findmyriding.app-Signed.apk`
- First launch extracts the bundled shapefiles into app data (cached under a
  `.extracted` marker). Uninstall the app for a clean asset test.

## APK size

Only the files the app reads are shipped as assets: per dataset it opens
`.shp`/`.dbf`/`.shx` (+`.cpg`), so the build excludes source archives
(`.zip`), duplicate encodings (`.geojson`/`.kml`), and metadata
(`.pdf`/`.docx`/`.xml`/`.sbn`/`.sbx`) from `data/Shapefiles/` (see the
`AndroidAsset` items in the csproj).

| Build | APK (signed) |
|---|---|
| Release, all assets (before) | 148,057,202 B (~148 MB) |
| Release, asset exclusions (after) | 78,423,690 B (~78 MB) |

Shapefiles make up ~62 MB of the resulting APK (14 datasets, `.shp`/`.dbf`/`.shx`/
  `.cpg`); the rest is the runtime, dex, and resources.

## AOT and trimming

- `RunAOTCompilation=false` is honored: no AOT runs (`PublishAot` is empty).
- `RunLinker=false` is a no-op on .NET 10: managed-assembly trimming is
  controlled by `PublishTrimmed`, which defaults to **true** on Release
  publishes, so ILLink runs (`--trim-mode link`). Measured on clean builds:
  trimmed 78,423,690 B vs untrimmed 98,174,602 B — trimming saves
  19,750,912 B (~19.75 MB). To actually disable it, set
  `<PublishTrimmed>false</PublishTrimmed>`; to make the current behavior
  explicit, set `<PublishTrimmed>true</PublishTrimmed>` and drop `RunLinker`.
- `lib/arm64-v8a/libassembly-store.so` in the APK is the Mono runtime's
  assembly container, not AOT output.

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
