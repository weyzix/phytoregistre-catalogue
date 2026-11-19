using System.Globalization;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Export;

/// <summary>Mise en forme fr-BE des valeurs du registre (virgule décimale, jj/mm/aaaa).</summary>
internal static class FrenchFormat
{
    /// <summary>
    /// Format numérique fr-BE (virgule décimale, pas de séparateur de milliers, signe « - » ASCII).
    /// Construit explicitement pour ne pas dépendre des données ICU (mode « invariant globalization »
    /// possible sur Android).
    /// </summary>
    private static readonly NumberFormatInfo Numbers = NumberFormatInfo.ReadOnly(new NumberFormatInfo
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = "",
        NegativeSign = "-",
    });

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";

    public static string Number(decimal value) => value.ToString("0.############", Numbers);

    public static string Number(double value) => value.ToString("0.##", Numbers);

    public static string Number(decimal? value) => value is { } v ? Number(v) : "";

    public static string Number(double? value) => value is { } v ? Number(v) : "";

    public static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    public static string Unit(QuantityUnit unit) => unit == QuantityUnit.Kilogram ? "kg" : "L";

    public static string Usage(UsageType usage) => usage switch
    {
        UsageType.OpenField => "Plein air",
        UsageType.UnderProtection => "Sous protection",
        UsageType.SeedTreatment => "Traitement de semences",
        _ => "Non précisé",
    };

    /// <summary>« LOT (quantité unité) » pour un traitement de semences / plants.</summary>
    public static string Seed(SeedTreatmentInfo? seed)
    {
        if (seed is null)
        {
            return "";
        }

        if (seed.Quantity is not { } qty)
        {
            return seed.LotNumber;
        }

        var unit = string.IsNullOrWhiteSpace(seed.QuantityUnit) ? "" : " " + seed.QuantityUnit;
        return $"{seed.LotNumber} ({Number(qty)}{unit})";
    }

    public static string Weather(WeatherConditions? weather)
    {
        if (weather is null)
        {
            return "";
        }

        var parts = new[]
        {
            Part(weather.TemperatureC, "", " °C"),
            Part(weather.WindSpeedKmh, "vent ", " km/h"),
            Part(weather.RelativeHumidityPct, "HR ", " %"),
            Part(weather.PrecipitationMm, "pluie ", " mm"),
        };
        var text = string.Join(" / ", parts.Where(p => p.Length > 0));
        return string.IsNullOrWhiteSpace(weather.Source) ? text : $"{text} ({weather.Source})".TrimStart();
    }

    /// <summary>Localisation principale : n° PAC, sinon cadastre, adresse ou x,y.</summary>
    public static string MainLocation(TreatmentLocation location)
    {
        var reference = FirstNonBlank(location.PacReference, location.CadastreNumber, location.Address)
            ?? (location.Coordinates is { } c ? $"x,y {Number(c.X)} / {Number(c.Y)}" : "");
        if (string.IsNullOrWhiteSpace(location.Label))
        {
            return reference;
        }

        return reference.Length == 0 ? location.Label : $"{location.Label} — {reference}";
    }

    /// <summary>« Client (ETA) » pour un traitement réalisé par un prestataire.</summary>
    public static string Contractor(ContractorInfo? contractor)
    {
        if (contractor is null)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(contractor.ContractorName)
            ? contractor.ClientName
            : $"{contractor.ClientName} ({contractor.ContractorName})";
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Part(double? value, string prefix, string suffix) =>
        value is { } v ? prefix + Number(v) + suffix : "";
}
