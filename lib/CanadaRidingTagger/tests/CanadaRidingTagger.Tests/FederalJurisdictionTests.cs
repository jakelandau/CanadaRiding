using CanadaRidingTagger;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Tests for the federal code two-digit prefix to <see cref="Jurisdiction"/> mapping.
/// The codes are the Elections Canada five-digit federal numbering scheme.
/// </summary>
public class FederalJurisdictionTests
{
    [Theory]
    [InlineData("10001", Jurisdiction.NewfoundlandLabrador)]
    [InlineData("10004", Jurisdiction.NewfoundlandLabrador)]
    [InlineData("11001", Jurisdiction.PEI)]
    [InlineData("12001", Jurisdiction.NovaScotia)]
    [InlineData("13001", Jurisdiction.NewBrunswick)]
    [InlineData("24037", Jurisdiction.Quebec)]
    [InlineData("24048", Jurisdiction.Quebec)]
    [InlineData("35001", Jurisdiction.Ontario)]
    [InlineData("35025", Jurisdiction.Ontario)]
    [InlineData("35522", Jurisdiction.Ontario)]
    [InlineData("46001", Jurisdiction.Manitoba)]
    [InlineData("47001", Jurisdiction.Saskatchewan)]
    [InlineData("48001", Jurisdiction.Alberta)]
    [InlineData("48037", Jurisdiction.Alberta)]
    [InlineData("59001", Jurisdiction.BritishColumbia)]
    [InlineData("59433", Jurisdiction.BritishColumbia)]
    [InlineData("60001", Jurisdiction.Yukon)]
    [InlineData("60002", Jurisdiction.Yukon)]
    [InlineData("61001", Jurisdiction.NorthwestTerritories)]
    [InlineData("62001", Jurisdiction.Nunavut)]
    public void FromId_MapsEachJurisdictionBlock(string id, Jurisdiction expected)
    {
        Assert.Equal(expected, FederalJurisdiction.FromId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("00")]
    [InlineData("20")]
    [InlineData("99")]
    [InlineData("Laurier—Sainte-Marie")]
    [InlineData("3")]
    public void FromId_UnknownPrefix_ReturnsNull(string? id)
    {
        Assert.Null(FederalJurisdiction.FromId(id));
    }
}
