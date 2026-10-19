using WorkerKit.Samples.Vies;

namespace WorkerKit.Samples.Tests;

public sealed class ViesVatNumberTests
{
    [Theory]
    [InlineData("de", "123456789", "DE", "123456789")]
    [InlineData(" DE ", "DE 123 456 789", "DE", "123456789")]
    [InlineData("fr", "FR12345678901", "FR", "12345678901")]
    public void Country_and_number_are_normalized(string country, string number, string expectedCountry, string expectedNumber)
    {
        Assert.Equal(expectedCountry, ViesVatNumber.NormalizeCountry(country));
        Assert.Equal(expectedNumber, ViesVatNumber.NormalizeNumber(country, number));
    }

    [Theory]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("D1")]
    public void Country_must_be_two_letters(string country)
    {
        Assert.Throws<ArgumentException>(() => ViesVatNumber.NormalizeCountry(country));
    }

    [Fact]
    public void Number_must_match_the_wsdl_pattern()
    {
        Assert.Throws<ArgumentException>(() => ViesVatNumber.NormalizeNumber("DE", "1"));
        Assert.Throws<ArgumentException>(() => ViesVatNumber.NormalizeNumber("DE", "1234567890123"));
    }
}
