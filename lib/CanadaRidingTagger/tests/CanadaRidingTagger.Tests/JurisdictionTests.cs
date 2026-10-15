using CanadaRidingTagger;
using Xunit;

namespace CanadaRidingTagger.Tests;

/// <summary>
/// Tests for <see cref="JurisdictionExtensions.DisplayName(Jurisdiction)"/>.
/// </summary>
public class JurisdictionTests
{
    [Theory]
    [InlineData(Jurisdiction.Alberta, "Alberta")]
    [InlineData(Jurisdiction.BritishColumbia, "British Columbia")]
    [InlineData(Jurisdiction.Manitoba, "Manitoba")]
    [InlineData(Jurisdiction.NewBrunswick, "New Brunswick")]
    [InlineData(Jurisdiction.NewfoundlandLabrador, "Newfoundland and Labrador")]
    [InlineData(Jurisdiction.NovaScotia, "Nova Scotia")]
    [InlineData(Jurisdiction.Ontario, "Ontario")]
    [InlineData(Jurisdiction.PEI, "Prince Edward Island")]
    [InlineData(Jurisdiction.Quebec, "Québec")]
    [InlineData(Jurisdiction.Saskatchewan, "Saskatchewan")]
    [InlineData(Jurisdiction.Yukon, "Yukon")]
    [InlineData(Jurisdiction.NorthwestTerritories, "Northwest Territories")]
    [InlineData(Jurisdiction.Nunavut, "Nunavut")]
    public void DisplayName_ReturnsExpectedName(Jurisdiction jurisdiction, string expected)
    {
        Assert.Equal(expected, jurisdiction.DisplayName());
    }
}
