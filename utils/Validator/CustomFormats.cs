using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Validator;

public static class CustomFormats
{
    private static readonly ImmutableSortedSet<string> CountryCodes;

    static CustomFormats()
    {
        CountryCodes = ISO3166.Country.List.Select(country => country.ThreeLetterCode).ToImmutableSortedSet(StringComparer.OrdinalIgnoreCase);
    }

    public static void RegisterAll()
    {
        Formats.Register(CountryCodeFormat);
    }

    private static readonly Format CountryCodeFormat = new PredicateFormat("ISO 3166-1 alpha-3", EvaluateCountryCode);

    private static bool EvaluateCountryCode(JsonNode? element, out string? errormessage)
    {
        errormessage = null;
        if (element is not JsonValue jsonValue)
        {
            errormessage = $"Node is not of type {nameof(jsonValue)}";
            return false;
        }

        if (!jsonValue.TryGetValue<string>(out var stringValue))
        {
            errormessage = "Value is not a string!";
            return false;
        }

        if (CountryCodes.Contains(stringValue)) return true;
        errormessage = $"'{stringValue}' is not a valid ISO 3166-1 alpha-3 country code!";
        return false;
    }
}
