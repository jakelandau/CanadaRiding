using CanadaRidingTagger;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Point-in-polygon tests for <see cref="RidingTagger"/>, over the real datasets.
/// Interior points and expected districts come from the fixtures'
/// <c>pip.csv</c> ground truth.
/// </summary>
public class RidingTaggerTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DataRoot = Path.Combine(RepoRoot, "Shapefiles");

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "Shapefiles", "Federal", "SHP")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Repo root not found");
    }

    // --- load guards -----------------------------------------------------

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var tagger = new RidingTagger();
        string path = Path.Combine(DataRoot, "DoesNotExist.shp");
        var source = new DistrictSource(path, new DatasetDefinition("RID", "NAME_EN", null), null);

        var ex = Assert.Throws<FileNotFoundException>(() => tagger.Load(source));

        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public void Load_UnsupportedExtension_ThrowsFormatException()
    {
        var tagger = new RidingTagger();
        string path = Path.Combine(Path.GetTempPath(), $"crtp_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "x");
            var source = new DistrictSource(path, new DatasetDefinition("RID", "NAME_EN", null), null);

            Assert.Throws<FormatException>(() => tagger.Load(source));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- federal only ----------------------------------------------------

    [Fact]
    public void FindRidings_FederalOnly_ReturnsFederalOnly()
    {
        var tagger = new RidingTagger();
        tagger.Load(StandardDatasets.Federal(DataRoot));

        // Ground truth: federal/pip.csv — id 24037, "Laurier—Sainte-Marie".
        var result = tagger.FindRidings(45.51480718414668, -73.55521122187946);

        Assert.NotNull(result.Federal);
        Assert.Equal("24037", result.Federal.Id);
        Assert.False(string.IsNullOrEmpty(result.Federal.NameEn));
        Assert.Null(result.Provincial);
        Assert.Null(result.Jurisdiction);
    }

    // --- federal + one province ------------------------------------------

    [Fact]
    public void FindRidings_Ottawa_ReturnsOntario()
    {
        var tagger = new RidingTagger();
        tagger.Load(StandardDatasets.Federal(DataRoot));
        tagger.Load(StandardDatasets.Source(DataRoot, Jurisdiction.Ontario));

        // Ground truth: on/pip.csv — id 8, "Brampton Centre".
        var result = tagger.FindRidings(43.6975888422089, -79.72916262149215);

        Assert.Equal(Jurisdiction.Ontario, result.Jurisdiction);
        Assert.NotNull(result.Provincial);
        Assert.Equal("Brampton Centre", result.Provincial.NameEn);
        Assert.NotNull(result.Federal);
    }

    [Fact]
    public void FindRidings_Vancouver_ReturnsBritishColumbia()
    {
        var tagger = new RidingTagger();
        tagger.Load(StandardDatasets.Federal(DataRoot));
        tagger.Load(StandardDatasets.Source(DataRoot, Jurisdiction.BritishColumbia));

        // Ground truth: bc/pip.csv — id 323, "Surrey City Centre".
        var result = tagger.FindRidings(49.191682470734634, -122.83697226163719);

        Assert.Equal(Jurisdiction.BritishColumbia, result.Jurisdiction);
        Assert.NotNull(result.Provincial);
        Assert.Equal("Surrey City Centre", result.Provincial.NameEn);
        Assert.NotNull(result.Federal);
    }

    [Fact]
    public void FindRidings_Offshore_ReturnsNulls()
    {
        var tagger = new RidingTagger();
        tagger.Load(StandardDatasets.Federal(DataRoot));
        tagger.Load(StandardDatasets.Source(DataRoot, Jurisdiction.Ontario));

        // Open Atlantic, far from any Canadian district.
        var result = tagger.FindRidings(30.0, -40.0);

        Assert.Null(result.Jurisdiction);
        Assert.Null(result.Federal);
        Assert.Null(result.Provincial);
    }

    // --- integration: all datasets, no cross-jurisdiction bleed ----------

    [Theory]
    [InlineData(Jurisdiction.Ontario, 43.6975888422089, -79.72916262149215, "Brampton Centre")]
    [InlineData(Jurisdiction.BritishColumbia, 49.191682470734634, -122.83697226163719, "Surrey City Centre")]
    public void FindRidings_AllDatasets_MatchGroundTruth(Jurisdiction jurisdiction, double lat, double lon, string expectedName)
    {
        var tagger = new RidingTagger();
        foreach (var source in StandardDatasets.All(DataRoot))
        {
            tagger.Load(source);
        }

        var result = tagger.FindRidings(lat, lon);

        Assert.Equal(jurisdiction, result.Jurisdiction);
        Assert.NotNull(result.Provincial);
        Assert.Equal(expectedName, result.Provincial.NameEn);
        Assert.NotNull(result.Federal);
    }
}
