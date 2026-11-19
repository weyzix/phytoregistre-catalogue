using System.Globalization;
using System.Text;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;
using PhytoRegistre.Core.Export;

namespace PhytoRegistre.Core.Catalog;

/// <summary>
/// Contrôles liés au produit du catalogue Fytoweb : validité de l'autorisation à la date
/// d'application, suspension, statut, public autorisé, culture et dose maximale.
/// Fonction pure ; avertissements indicatifs, l'acte d'autorisation fait foi.
/// </summary>
public sealed class ProductComplianceChecker : IProductComplianceChecker
{
    public const string Withdrawn = "PRODUCT_WITHDRAWN";
    public const string Suspended = "PRODUCT_SUSPENDED";
    public const string Provisional = "PRODUCT_PROVISIONAL";
    public const string NotProfessional = "PRODUCT_NOT_PROFESSIONAL";
    public const string DoseAboveMax = "DOSE_ABOVE_MAX";
    public const string CropNotInAuthorization = "CROP_NOT_IN_AUTHORIZATION";

    private const string ProductField = nameof(TreatmentRecord.ProductName);

    public IReadOnlyList<ValidationIssue> Check(TreatmentRecord record, Product product)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(product);

        return
        [
            .. CheckValidity(record.ApplicationDate, product),
            .. CheckSuspension(record.ApplicationDate, product),
            .. CheckStatus(product),
            .. CheckCropAndDose(record, product),
        ];
    }

    /// <summary>Minuscules, sans accents, apostrophes typographiques unifiées, espaces réduits.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var decomposed = text.Trim().ToLowerInvariant().Replace('’', '\'').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var words = builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words);
    }

    private static IEnumerable<ValidationIssue> CheckValidity(DateOnly date, Product p)
    {
        var limit = p.UseToleratedUntil ?? p.AuthorizedUntil;
        if (limit is { } end && date > end)
        {
            yield return new(Severity.Error, ProductField, Withdrawn,
                $"Autorisation du produit retirée ou expirée : utilisation permise jusqu'au {FrenchFormat.Date(end)} seulement.");
        }
        else if (p.Status == ProductStatus.Withdrawn)
        {
            var until = limit is { } l ? $" jusqu'au {FrenchFormat.Date(l)}" : "";
            yield return new(Severity.Warning, ProductField, Withdrawn,
                $"Autorisation du produit retirée : utilisation encore tolérée{until} (délai de grâce).");
        }
    }

    private static IEnumerable<ValidationIssue> CheckSuspension(DateOnly date, Product p)
    {
        if (p.SuspendedFrom is { } from && date >= from && (p.SuspendedUntil is not { } until || date <= until))
        {
            var period = p.SuspendedUntil is { } u ? $"du {FrenchFormat.Date(from)} au {FrenchFormat.Date(u)}" : $"depuis le {FrenchFormat.Date(from)}";
            yield return new(Severity.Error, ProductField, Suspended,
                $"Autorisation du produit suspendue {period} : utilisation interdite pendant la suspension.");
        }
    }

    private static IEnumerable<ValidationIssue> CheckStatus(Product p)
    {
        if (p.Status == ProductStatus.Provisional)
        {
            yield return new(Severity.Info, ProductField, Provisional,
                "Autorisation provisoire : vérifiez les conditions d'utilisation sur fytoweb.be.");
        }

        if (!p.IsProfessional)
        {
            yield return new(Severity.Warning, ProductField, NotProfessional,
                "Produit autorisé uniquement pour l'usage amateur (non professionnel).");
        }
    }

    private static IEnumerable<ValidationIssue> CheckCropAndDose(TreatmentRecord r, Product p)
    {
        var crop = Normalize(r.CropName);
        if (crop.Length == 0 || p.Usages is not { Count: > 0 })
        {
            return [];
        }

        var matching = p.Usages.Where(u => CropMatches(crop, u)).ToList();
        if (matching.Count == 0)
        {
            return
            [
                new(Severity.Info, nameof(TreatmentRecord.CropName), CropNotInAuthorization,
                    $"La culture « {r.CropName!.Trim()} » ne figure pas parmi les usages autorisés de ce produit (vérifiez l'intitulé exact)."),
            ];
        }

        return CheckDose(r, matching);
    }

    private static bool CropMatches(string crop, ProductUsage usage)
    {
        var name = Normalize(usage.CropNameFr);
        var scientific = Normalize(usage.CropScientificName);
        return ContainsEitherWay(crop, name) || ContainsEitherWay(crop, scientific);
    }

    private static bool ContainsEitherWay(string a, string b) =>
        a.Length > 0 && b.Length > 0 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));

    private static IEnumerable<ValidationIssue> CheckDose(TreatmentRecord r, IEnumerable<ProductUsage> usages)
    {
        if (r.DosePerHa <= 0)
        {
            yield break;
        }

        var maxima = usages
            .Select(u => DoseUnits.ToRecordUnit(u.DoseMax, u.DoseUnit, r.DoseUnit))
            .OfType<decimal>()
            .ToList();
        if (maxima.Count > 0 && r.DosePerHa > maxima.Max())
        {
            var unit = FrenchFormat.Unit(r.DoseUnit);
            yield return new(Severity.Warning, nameof(TreatmentRecord.DosePerHa), DoseAboveMax,
                $"Dose de {FrenchFormat.Number(r.DosePerHa)} {unit}/ha supérieure à la dose maximale autorisée "
                + $"pour cette culture ({FrenchFormat.Number(maxima.Max())} {unit}/ha).");
        }
    }
}
