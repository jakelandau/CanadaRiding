using CanadaRidingTagger;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Map-vintage tests for <see cref="RidingTagger"/> over the real datasets:
/// the current maps plus the bundled past vintages (NS 2021, YT 2021, QC 2022).
/// Test points were chosen so the answer differs between vintages; ground truth
/// comes from <c>tools/ground_truth_vintages.py</c>.
/// </summary>
public class VintageTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DataRoot = Path.Combine(RepoRoot, "data", "Shapefiles");

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "lib", "CanadaRidingTagger")) &&
                Directory.Exists(Path.Combine(dir, "data", "Shapefiles", "Federal", "SHP")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Repo root not found");
    }

    private static RidingTagger LoadAllVintages()
    {
        var tagger = new RidingTagger();
        foreach (var source in StandardDatasets.AllVintages(DataRoot))
        {
            tagger.Load(source);
        }
        return tagger;
    }

    // --- vintage enumeration ---------------------------------------------

    [Theory]
    [InlineData(null, new int[] { 2025 })]
    [InlineData(Jurisdiction.Ontario, new int[] { 2022 })]
    [InlineData(Jurisdiction.Alberta, new int[] { 2019 })]
    [InlineData(Jurisdiction.Yukon, new int[] { 2021, 2025 })]
    [InlineData(Jurisdiction.NovaScotia, new int[] { 2021, 2026 })]
    [InlineData(Jurisdiction.Quebec, new int[] { 2022, 2026 })]
    public void Vintages_ReturnsLoadedYearsAscending(Jurisdiction? jurisdiction, int[] expected)
    {
        var tagger = LoadAllVintages();

        Assert.Equal(expected, tagger.Vintages(jurisdiction));
        Assert.Equal(expected[^1], tagger.CurrentYear(jurisdiction));
    }

    // --- selecting a past vintage changes the answer -----------------------

    [Theory]
    [InlineData(45.682046, -72.5140195, 2022, "Johnson", "Daniel-Johnson")]
    [InlineData(45.4921395, -73.48288021, 2022, "Laporte", "Pierre-Laporte")]
    [InlineData(46.33549769, -61.08356408, 2021, "Inverness", "Chéticamp-Margarees-Pleasant Bay")]
    [InlineData(60.7275226954, -135.0614411764, 2021, "Whitehorse Centre", "Takhini")]
    public void FindRidings_PastVintage_ReturnsPastRiding(double lat, double lon, int year, string pastName, string currentName)
    {
        var tagger = LoadAllVintages();

        var pastResult = tagger.FindRidings(lat, lon, provincialYear: year);
        Assert.NotNull(pastResult.Provincial);
        Assert.Equal(pastName, pastResult.Provincial.NameEn);

        var currentResult = tagger.FindRidings(lat, lon);
        Assert.NotNull(currentResult.Provincial);
        Assert.Equal(currentName, currentResult.Provincial.NameEn);
    }

    // --- explicit vintage not loaded ---------------------------------------

    [Fact]
    public void FindRidings_UnloadedFederalYear_ThrowsArgumentException()
    {
        var tagger = LoadAllVintages();

        // Only the 2025 federal map is bundled; 2019 is not loaded.
        Assert.Throws<ArgumentException>(
            () => tagger.FindRidings(45.5, -73.6, federalYear: 2019));
    }
}
