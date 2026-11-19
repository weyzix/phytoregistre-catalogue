using System.Text.RegularExpressions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>
/// Règles de dérivation des indicateurs à partir de l'export Fytoweb. Ces règles sont des
/// approximations documentées : elles ne remplacent pas l'acte d'autorisation.
/// </summary>
public static partial class FytowebHeuristics
{
    public const string Spe8Code = "SPe8";

    /// <summary>
    /// Formulations solides (codes CIPAC/GIFAP) : la dose s'exprime en kg.
    /// GR granulés, WG granulés dispersables, WP poudre mouillable, SG/SP granulés/poudre solubles,
    /// DP poudre pour poudrage, RB appât, MG microgranulés, TB/WT/ST tablettes, DS/WS poudres pour semences,
    /// FG/GB/CB granulés fins/appâts, FU fumigène, PA pâte.
    /// </summary>
    private static readonly HashSet<string> SolidFormulations = new(StringComparer.OrdinalIgnoreCase)
    {
        "GR", "WG", "WP", "SG", "SP", "DP", "RB", "MG", "TB", "WT", "ST", "DS", "WS", "FG", "GB", "CB", "FU", "PA",
    };

    /// <summary>AKK = accord, INT = retrait, PROVISIONAL_AGREEMENT = accord provisoire ; autre → Unknown.</summary>
    public static ProductStatus StatusFromDecision(string? decisionCode) => decisionCode?.Trim().ToUpperInvariant() switch
    {
        "AKK" => ProductStatus.Authorized,
        "INT" => ProductStatus.Withdrawn,
        "PROVISIONAL_AGREEMENT" => ProductStatus.Provisional,
        _ => ProductStatus.Unknown,
    };

    public static bool IsSolidFormulation(string? formulationCode)
    {
        var code = formulationCode?.Split([' ', '-', '('], 2)[0].Trim();
        return !string.IsNullOrEmpty(code) && SolidFormulations.Contains(code);
    }

    /// <summary>Numérateur d'une unité de dose (« kg/ha » → kg) : masse, volume ou autre.</summary>
    public static DoseQuantityKind QuantityKind(string? doseUnit)
    {
        var numerator = doseUnit?.Split('/', 2)[0].Trim().ToLowerInvariant();
        return numerator switch
        {
            "kg" or "g" or "mg" => DoseQuantityKind.Mass,
            "l" or "ml" or "µl" or "μl" or "cl" => DoseQuantityKind.Volume,
            _ => DoseQuantityKind.Other,
        };
    }

    /// <summary>Kilogramme si la majorité des unités de dose sont massiques ou si la formulation est solide.</summary>
    public static QuantityUnit ProductUnit(IEnumerable<string?> doseUnits, string? formulationCode)
    {
        var kinds = doseUnits.Select(QuantityKind).ToList();
        var mass = kinds.Count(k => k == DoseQuantityKind.Mass);
        var volume = kinds.Count(k => k == DoseQuantityKind.Volume);
        return mass > volume || IsSolidFormulation(formulationCode) ? QuantityUnit.Kilogram : QuantityUnit.Liter;
    }

    /// <summary>
    /// Heuristique : un produit SPe8 a une restriction horaire quand son texte spécifique (customTextFr)
    /// évoque une plage horaire (« tôt le matin », « tard le soir », « heure », « 20h00 », « coucher du soleil »…).
    /// La liste officielle des produits à restriction horaire est publiée séparément par Fytoweb (PDF) :
    /// elle fait foi en cas de divergence.
    /// </summary>
    public static bool MentionsTimeWindow(string? text) => !string.IsNullOrWhiteSpace(text) && TimeWindow().IsMatch(text);

    [GeneratedRegex(
        @"t[ôo]t le matin|tard (le|dans la) soir|en soir[ée]e|heures?\b|\b\d{1,2}\s?h\s?\d{0,2}\b|coucher du soleil|lever du soleil|apr[èe]s le vol des abeilles",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeWindow();

    /// <summary>Le texte de stade mentionne explicitement une échelle BBCH.</summary>
    public static bool MentionsBbch(string? growthStage) =>
        growthStage?.Contains("BBCH", StringComparison.OrdinalIgnoreCase) == true;
}

public enum DoseQuantityKind
{
    Other = 0,
    Mass = 1,
    Volume = 2,
}
