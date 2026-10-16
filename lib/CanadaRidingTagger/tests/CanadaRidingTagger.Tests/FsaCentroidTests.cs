using CanadaRidingTagger;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Tests for <see cref="FsaCentroids"/> and <see cref="RidingTagger.FindRidingsByFsa(string)"/>.
/// The FSA table is bundled as an embedded assembly resource, so none of these
/// tests touches a file path or the network; the coordinate values are the
/// geoview-api-geolocator FSA centroids (verbatim).
/// </summary>
public class FsaCentroidTests
{
    private static readonly string DataRoot = Path.Combine(FindRepoRoot(), "data", "Shapefiles");

    // --- bundled table ---------------------------------------------------

    [Fact]
    public void Table_IsBundledWithTheAssembly()
    {
        var assembly = typeof(FsaCentroids).Assembly;
        Assert.Contains("CanadaRidingTagger.FsaCentroids.csv", assembly.GetManifestResourceNames());
        // The bundled table is the full 2021 Census CFSA list (1,643 areas).
        Assert.Equal(1643, FsaCentroids.Count);
    }

    // --- conversion ------------------------------------------------------

    [Theory]
    [InlineData("K1S", 45.401193, -75.687205)]
    [InlineData("K1A", 45.443442, -75.619291)]
    [InlineData("k1s", 45.401193, -75.687205)]
    [InlineData(" K1S ", 45.401193, -75.687205)]
    [InlineData("K1S 0B1", 45.401193, -75.687205)]
    [InlineData("k1s0b1", 45.401193, -75.687205)]
    public void Get_ReturnsTheFsaCentroid(string fsa, double latitude, double longitude)
    {
        var (lat, lon) = FsaCentroids.Get(fsa);
        Assert.Equal(latitude, lat, 10);
        Assert.Equal(longitude, lon, 10);
    }

    [Fact]
    public void Get_Malformed_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FsaCentroids.Get(null!));
        Assert.Throws<FormatException>(() => FsaCentroids.Get("K1"));
        Assert.Throws<FormatException>(() => FsaCentroids.Get("K1A0"));
        Assert.Throws<FormatException>(() => FsaCentroids.Get("111"));
        Assert.Throws<FormatException>(() => FsaCentroids.Get("K1S 0"));
    }

    [Fact]
    public void Get_WellFormedFsaNotInTable_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => FsaCentroids.Get("A9A"));
    }

    [Fact]
    public void TryGet_NotInTable_ReturnsFalse()
    {
        Assert.False(FsaCentroids.TryGet("A9A", out _, out _));
        Assert.True(FsaCentroids.TryGet("K1S", out _, out _));
    }

    // --- end-to-end: FSA to ridings, reusing the coordinate search --------

    [Fact]
    public void FindRidingsByFsa_K1A_ReturnsOttawaVanierGloucester()
    {
        var tagger = NewTagger();
        var result = tagger.FindRidingsByFsa("K1A");

        Assert.Equal(Jurisdiction.Ontario, result.Jurisdiction);
        Assert.NotNull(result.Federal);
        Assert.Equal("Ottawa—Vanier—Gloucester", result.Federal.NameEn);
    }

    [Fact]
    public void FindRidingsByFsa_FullPostalCode_MatchesFirstThree()
    {
        var tagger = NewTagger();
        Assert.Equal(tagger.FindRidingsByFsa("K1A"), tagger.FindRidingsByFsa("K1A 0B1"));
    }

    [Fact]
    public void FindRidingsByFsa_MatchesFindRidings_AtTheCentroid()
    {
        var tagger = NewTagger();
        var (lat, lon) = FsaCentroids.Get("K1S");

        Assert.Equal(tagger.FindRidings(lat, lon), tagger.FindRidingsByFsa("K1S"));
    }

    private static RidingTagger NewTagger()
    {
        var tagger = new RidingTagger();
        foreach (var source in StandardDatasets.All(DataRoot))
        {
            tagger.Load(source);
        }
        return tagger;
    }

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
}
