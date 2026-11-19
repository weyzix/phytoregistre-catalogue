using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Catalog;

/// <summary>Conversion des doses Fytoweb par hectare (« l/ha », « ml/ha », « kg/ha », « g/ha ») vers l'unité de la fiche.</summary>
public static class DoseUnits
{
    /// <summary>
    /// Dose convertie en L/ha ou kg/ha selon <paramref name="recordUnit"/>, ou null si l'unité Fytoweb
    /// n'est pas comparable (autre surface que l'hectare, unité de volume vs masse, unité inconnue).
    /// </summary>
    public static decimal? ToRecordUnit(decimal? dose, string? fytowebUnit, QuantityUnit recordUnit)
    {
        if (dose is not { } value || Factor(fytowebUnit, recordUnit) is not { } factor)
        {
            return null;
        }

        return value * factor;
    }

    private static decimal? Factor(string? unit, QuantityUnit recordUnit) =>
        (unit?.Trim().ToLowerInvariant(), recordUnit) switch
        {
            ("l/ha", QuantityUnit.Liter) => 1m,
            ("ml/ha", QuantityUnit.Liter) => 0.001m,
            ("kg/ha", QuantityUnit.Kilogram) => 1m,
            ("g/ha", QuantityUnit.Kilogram) => 0.001m,
            _ => null,
        };
}
