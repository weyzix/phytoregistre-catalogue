using System.Text.RegularExpressions;

namespace PhytoRegistre.Core.Validation;

/// <summary>
/// Format des n° d'autorisation belges : <c>nnnnP/B</c>, <c>P/P</c>, <c>G/B</c>, <c>G/P</c>
/// (PPP) ou <c>BS_nnnn</c> (substance de base). Biocides et macro-organismes ne sont pas couverts.
/// </summary>
public static partial class AuthorizationNumber
{
    [GeneratedRegex(@"^\d{1,6}[PG]/[BP]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PppPattern();

    [GeneratedRegex(@"^BS_\d{1,5}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BasicSubstancePattern();

    /// <summary>Supprime les espaces de début/fin et met en majuscules.</summary>
    public static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool IsValid(string? value)
    {
        var normalized = Normalize(value);
        return PppPattern().IsMatch(normalized) || BasicSubstancePattern().IsMatch(normalized);
    }

    public static bool IsBasicSubstance(string? value) =>
        BasicSubstancePattern().IsMatch(Normalize(value));
}
