using System.Text.RegularExpressions;

namespace WorkerKit.Samples.Vies;

/// <summary>
/// Country code and VAT number as the VIES WSDL describes them:
/// country <c>[A-Z]{2}</c>, number <c>[0-9A-Za-z+*.]{2,12}</c>.
/// </summary>
public static partial class ViesVatNumber
{
    public static string NormalizeCountry(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        var country = countryCode.Trim().ToUpperInvariant();
        if (!Country().IsMatch(country))
        {
            throw new ArgumentException("Country code must be two ASCII letters.", nameof(countryCode));
        }

        return country;
    }

    public static string NormalizeNumber(string countryCode, string vatNumber)
    {
        var country = NormalizeCountry(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(vatNumber);
        var number = vatNumber.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (number.StartsWith(country, StringComparison.OrdinalIgnoreCase) && number.Length > country.Length)
        {
            number = number[country.Length..];
        }

        if (!Number().IsMatch(number))
        {
            throw new ArgumentException(
                "VAT number must be 2 to 12 characters from digits, letters, '+', '*', or '.'.",
                nameof(vatNumber));
        }

        return number;
    }

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Country();

    [GeneratedRegex("^[0-9A-Za-z+*.]{2,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex Number();
}
