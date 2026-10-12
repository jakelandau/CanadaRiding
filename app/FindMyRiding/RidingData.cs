using Android.Content.Res;
using Microsoft.Maui.Storage;

namespace FindMyRidingApp;

/// <summary>
/// Extracts the bundled "Shapefiles" Android assets into a writable app-data
/// directory so the shapefile/geojson readers (which use File/Path) can see them.
/// Result is cached by a marker file and reused across launches.
/// </summary>
internal static class RidingData
{
    const string AssetRoot = "Shapefiles";

    public static string EnsureDataRoot()
    {
        string dataRoot = Path.Combine(FileSystem.AppDataDirectory, AssetRoot);
        string marker = Path.Combine(dataRoot, ".extracted");
        if (File.Exists(marker))
        {
            return dataRoot;
        }

        CopyTree(Android.App.Application.Context.Assets, AssetRoot, dataRoot);
        RenameQuebecDir(dataRoot);
        File.WriteAllText(marker, "ok");
        return dataRoot;
    }

    static void CopyTree(AssetManager assets, string assetDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string entry in assets.List(assetDir))
        {
            string name = entry.TrimEnd('/');
            string assetPath = assetDir.Length == 0 ? name : assetDir + "/" + name;
            string dest = Path.Combine(destDir, name);
            try
            {
                using Stream src = assets.Open(assetPath);
                using Stream dst = File.Create(dest);
                src.CopyTo(dst);
            }
            catch (Java.IO.FileNotFoundException)
            {
                // Open throws on directories (this API level's List adds no trailing '/'): recurse.
                CopyTree(assets, assetPath, dest);
            }
        }
    }

    // The QC shapefiles are bundled under an ASCII directory name because AAPT2 cannot open the
    // non-ASCII é path on Windows. Restore the é name the library's StandardDatasets expects.
    static void RenameQuebecDir(string dataRoot)
    {
        string qcAscii = Path.Combine(dataRoot, "Provincial", "QC", "qc_electorales_ascii");
        string qcReal = Path.Combine(dataRoot, "Provincial", "QC", "circonscriptions_\u00e9lectorales_2026_shapefile");
        if (!Directory.Exists(qcAscii))
            return;
        if (Directory.Exists(qcReal))
            Directory.Delete(qcReal, true);
        Directory.Move(qcAscii, qcReal);
    }
}
