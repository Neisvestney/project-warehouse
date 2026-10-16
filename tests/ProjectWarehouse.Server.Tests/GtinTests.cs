using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Tests;

public class GtinTests
{
    [Theory]
    [InlineData("04607012345677", "04607012345677")]
    [InlineData("4607012345677", "04607012345677")]
    [InlineData(" 4607012345677 ", "04607012345677")]
    [InlineData("036000291452", "00036000291452")]
    [InlineData("96385074", "00000096385074")]
    public void Normalize_PadsValidGtinTo14Digits(string input, string expected) =>
        Assert.Equal(expected, Gtin.Normalize(input));

    [Theory]
    [InlineData("4607012345672")]
    [InlineData("460701234567")]
    [InlineData("46070123456A1")]
    [InlineData("")]
    [InlineData("010460701234567721abc")]
    public void Normalize_RejectsInvalidGtin(string input) =>
        Assert.Null(Gtin.Normalize(input));
}
