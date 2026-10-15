using CanadaRidingTagger;
using CanadaRidingTagger.Readers;
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
        // Jurisdiction is now determined from the federal code (24xxx -> Quebec) even though
        // no subnational dataset is loaded; only the provincial district stays null.
        Assert.Equal(Jurisdiction.Quebec, result.Jurisdiction);
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

    [Fact]
    public void Load_AllStandardDatasets_EachYieldsDistricts()
    {
        var sources = StandardDatasets.All(DataRoot);
        Assert.Equal(14, sources.Count);
        foreach (var source in sources)
        {
            var districts = DistrictReader.Read(source);
            Assert.True(districts.Count > 0, $"Dataset for {source.Jurisdiction?.ToString() ?? "Federal"} yielded zero districts");
        }
    }

    [Theory]
    [InlineData(Jurisdiction.Ontario, 43.6975888422089, -79.72916262149215, "Brampton Centre")]
    [InlineData(Jurisdiction.BritishColumbia, 49.191682470734634, -122.83697226163719, "Surrey City Centre")]
    [InlineData(Jurisdiction.Alberta, 49.68854339828085, -112.87923898609279, "Lethbridge-West")]
    [InlineData(Jurisdiction.Manitoba, 49.81450315251009, -97.09417236572575, "Seine River")]
    [InlineData(Jurisdiction.Saskatchewan, 57.07439241934494, -107.50384957529023, "Athabasca")]
    [InlineData(Jurisdiction.NewBrunswick, 47.687915592451176, -67.07800918215372, "Restigouche West")]
    [InlineData(Jurisdiction.NovaScotia, 44.78013593300412, -65.27046160871136, "Annapolis")]
    [InlineData(Jurisdiction.NewfoundlandLabrador, 49.75059168972005, -56.053686829989, "Baie Verte - Green Bay")]
    [InlineData(Jurisdiction.Nunavut, 62.53217122449641, -72.48632431028307, "SOUTH BAFFIN")]
    [InlineData(Jurisdiction.NorthwestTerritories, 64.46718976688705, -116.24999999973872, "Monfwi")]
    [InlineData(Jurisdiction.Yukon, 60.77768005685998, -135.0991000759725, "Whistle Bend South")]
    [InlineData(Jurisdiction.PEI, 46.08125536161161, -62.74483107031237, "Belfast - Murray River")]
    [InlineData(Jurisdiction.Quebec, 45.49280435374364, -73.4001782306937, "Vachon")]
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

    // Regression: at (52.08, -60.69) the Quebec shapefile's polygon spuriously covers
    // the point alongside the Newfoundland one. Jurisdiction must come from the
    // federal code (10004 -> NL), not from whichever provincial dataset is loaded last.
    [Fact]
    public void FindRidings_LabradorPoint_ReturnsNewfoundlandNotQuebec()
    {
        var tagger = new RidingTagger();
        foreach (var source in StandardDatasets.All(DataRoot))
        {
            tagger.Load(source);
        }

        var result = tagger.FindRidings(52.08, -60.69);

        Assert.NotNull(result.Federal);
        Assert.Equal("Labrador", result.Federal.NameEn);
        Assert.Equal(Jurisdiction.NewfoundlandLabrador, result.Jurisdiction);
        Assert.NotNull(result.Provincial);
        Assert.Equal("Lake Melville", result.Provincial.NameEn);
    }
}
