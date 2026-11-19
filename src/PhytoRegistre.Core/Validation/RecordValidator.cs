using System.Text.RegularExpressions;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;
using PhytoRegistre.Core.Export;
using PhytoRegistre.Core.Rules;

namespace PhytoRegistre.Core.Validation;

/// <summary>
/// Valide une fiche selon les 8 rubriques du Règl. (UE) 2023/564.
/// Fonction pure : la fiche n'est jamais modifiée.
/// </summary>
public sealed partial class RecordValidator(IDeadlineCalculator deadlines) : IRecordValidator
{
    public const decimal MaxRealisticDosePerHa = 100m;
    public const int MinBbch = 0;
    public const int MaxBbch = 99;

    // Emprise approximative de la Belgique en Lambert belge 72 (m).
    private const double LambertMin = 0;
    private const double LambertMax = 300_000;

    public RecordValidator() : this(new DeadlineCalculator())
    {
    }

    [GeneratedRegex("^[A-Z0-9]{5,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex EppoPattern();

    public IReadOnlyList<ValidationIssue> Validate(TreatmentRecord record, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(record);

        return
        [
            .. ValidateUsageType(record),
            .. ValidateProduct(record),
            .. ValidateDose(record),
            .. ValidateArea(record),
            .. ValidateLocation(record),
            .. ValidateContractor(record),
            .. ValidateMoment(record, today),
            .. ValidateEppo(record),
            .. ValidateBbch(record),
        ];
    }

    private static bool IsSeedTreatment(TreatmentRecord r) => r.UsageType == UsageType.SeedTreatment;

    private static ValidationIssue Error(string field, string code, string message) =>
        new(Severity.Error, field, code, message);

    private static ValidationIssue Warning(string field, string code, string message) =>
        new(Severity.Warning, field, code, message);

    private static ValidationIssue Info(string field, string code, string message) =>
        new(Severity.Info, field, code, message);

    // 1. Type d'utilisation
    private static IEnumerable<ValidationIssue> ValidateUsageType(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.UsageType);
        if (r.UsageType == UsageType.Unspecified)
        {
            yield return Warning(field, "USAGE_TYPE_MISSING",
                "Type d'utilisation non précisé (plein air, sous protection, traitement de semences). "
                + "Facultatif uniquement s'il se déduit des autres données de la fiche.");
        }
        else if (r.Seed is not null && r.UsageType != UsageType.SeedTreatment)
        {
            yield return Warning(field, "USAGE_TYPE_SEED_INCONSISTENT",
                "Des informations de traitement de semences sont saisies mais le type d'utilisation "
                + "n'est pas « traitement de semences / plants ».");
        }
    }

    // 2. Produit
    private static IEnumerable<ValidationIssue> ValidateProduct(TreatmentRecord r)
    {
        if (string.IsNullOrWhiteSpace(r.ProductName))
        {
            yield return Error(nameof(TreatmentRecord.ProductName), "PRODUCT_NAME_REQUIRED",
                "Le nom commercial complet du produit est obligatoire.");
        }

        if (!AuthorizationNumber.IsValid(r.AuthorizationNumber))
        {
            yield return Error(nameof(TreatmentRecord.AuthorizationNumber), "PRODUCT_AUTH_INVALID",
                "N° d'autorisation absent ou invalide : formats attendus nnnnP/B, P/P, G/B, G/P "
                + "ou BS_nnnn (substance de base). Les biocides (biocide.be) et les macro-organismes "
                + "ne relèvent pas de ce registre.");
        }

        if (!string.IsNullOrWhiteSpace(r.EmergencyAuthorizationNumber))
        {
            yield return Info(nameof(TreatmentRecord.EmergencyAuthorizationNumber), "EMERGENCY_AUTH_COMMENT",
                "Autorisation d'urgence : le n° d'urgence est conservé en commentaire ; "
                + "le n° d'autorisation du produit en stock est utilisé.");
        }
    }

    // 3. Dose
    private static IEnumerable<ValidationIssue> ValidateDose(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.DosePerHa);
        if (r.DosePerHa <= 0)
        {
            yield return Error(field, "DOSE_INVALID",
                "La dose (quantité de produit en kg ou L par ha) doit être supérieure à 0.");
        }
        else if (r.DosePerHa > MaxRealisticDosePerHa)
        {
            yield return Warning(field, "DOSE_UNREALISTIC",
                $"Dose supérieure à {MaxRealisticDosePerHa} par ha : vérifiez l'unité et la valeur saisie.");
        }

        if (r.ProductUnit is { } productUnit && productUnit != r.DoseUnit)
        {
            yield return Error(nameof(TreatmentRecord.DoseUnit), "DOSE_UNIT_MISMATCH",
                $"Unité de dose ({FrenchFormat.Unit(r.DoseUnit)}/ha) différente de l'unité du produit "
                + $"({FrenchFormat.Unit(productUnit)}) : la dose doit être exprimée dans l'unité du produit.");
        }
    }


    // 4. Surface traitée
    private static IEnumerable<ValidationIssue> ValidateArea(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.TreatedAreaHa);
        if (r.TreatedAreaHa <= 0)
        {
            // Traitement partiel / spot spraying : c'est la surface réellement traitée qui compte.
            yield return Error(field, "AREA_INVALID",
                "La surface réellement traitée (ha) doit être supérieure à 0.");
        }
    }

    // 5. Localisation
    private static IEnumerable<ValidationIssue> ValidateLocation(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.Location);
        if (IsSeedTreatment(r))
        {
            foreach (var issue in ValidateSeed(r.Seed))
            {
                yield return issue;
            }
        }
        else if (!r.Location.HasLocatingReference)
        {
            yield return Error(field, "LOCATION_REQUIRED",
                "Localisation obligatoire : n° de parcelle PAC, sinon n° cadastral, adresse ou coordonnées x,y "
                + "(un simple nom de parcelle ne suffit pas).");
        }

        foreach (var issue in ValidateLocationDetails(r.Location))
        {
            yield return issue;
        }
    }

    // Traitement de semences / plants : le lot et la quantité traitée remplacent la parcelle.
    private static IEnumerable<ValidationIssue> ValidateSeed(SeedTreatmentInfo? seed)
    {
        const string field = nameof(TreatmentRecord.Seed);
        if (string.IsNullOrWhiteSpace(seed?.LotNumber))
        {
            yield return Error(field, "SEED_LOT_REQUIRED",
                "Traitement de semences / plants : le n° de lot des semences ou plants est obligatoire.");
        }

        if (seed?.Quantity is not > 0)
        {
            yield return Error(field, "SEED_QUANTITY_REQUIRED",
                "Traitement de semences / plants : la quantité de semences ou plants traitée doit être supérieure à 0.");
        }
    }

    private static IEnumerable<ValidationIssue> ValidateLocationDetails(TreatmentLocation location)
    {
        const string field = nameof(TreatmentRecord.Location);

        if (string.IsNullOrWhiteSpace(location.PacReference) && location.HasLocatingReference)
        {
            yield return Info(field, "LOCATION_PAC_PREFERRED",
                "Localisation sans n° PAC : à remplacer par le n° de parcelle PAC dès qu'il est connu.");
        }

        if (location.Coordinates is { } p && !IsInsideBelgium(p))
        {
            yield return Warning(field, "COORDINATES_OUT_OF_BOUNDS",
                $"Coordonnées x={p.X:0}, y={p.Y:0} hors de la Belgique (Lambert belge 72) : vérifiez la saisie.");
        }
    }

    private static bool IsInsideBelgium(LambertPoint p) =>
        p.X is >= LambertMin and <= LambertMax && p.Y is >= LambertMin and <= LambertMax;

    private static IEnumerable<ValidationIssue> ValidateContractor(TreatmentRecord r)
    {
        if (r.Contractor is not null && string.IsNullOrWhiteSpace(r.Contractor.ClientName))
        {
            yield return Error(nameof(TreatmentRecord.Contractor), "CONTRACTOR_CLIENT_REQUIRED",
                "Traitement par une ETA / un trieur à façon : le nom du client est obligatoire.");
        }
    }

    // 6. Moment
    private IEnumerable<ValidationIssue> ValidateMoment(TreatmentRecord r, DateOnly today)
    {
        const string field = nameof(TreatmentRecord.ApplicationDate);
        if (r.ApplicationDate == default)
        {
            yield return Error(field, "DATE_REQUIRED", "La date d'application est obligatoire.");
        }
        else if (r.ApplicationDate > today)
        {
            yield return Error(field, "DATE_IN_FUTURE", "La date d'application ne peut pas être dans le futur.");
        }
        else if (DateOnly.FromDateTime(r.CreatedAt.Date) > deadlines.Compute(r).RegistrationDeadline)
        {
            yield return Warning(nameof(TreatmentRecord.CreatedAt), "REGISTERED_LATE",
                "Fiche enregistrée plus de 7 jours après l'application (délai légal : 7 jours).");
        }

        if (r.ProductHasTimeRestriction && r.StartTime is null)
        {
            yield return Error(nameof(TreatmentRecord.StartTime), "START_TIME_REQUIRED",
                "Produit à restriction horaire (SPe8 abeilles) : l'heure de début du traitement est obligatoire.");
        }
    }

    // 7. Culture / code OEPP
    private static IEnumerable<ValidationIssue> ValidateEppo(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.EppoCode);
        if (string.IsNullOrWhiteSpace(r.EppoCode))
        {
            if (r.CropCategory is CropCategory.Fallow or CropCategory.Uncultivated)
            {
                yield break; // pas de culture : pas de code OEPP
            }

            yield return Warning(field, "EPPO_MISSING", "Code OEPP de la culture non renseigné.");
        }
        else if (!EppoPattern().IsMatch(r.EppoCode.Trim().ToUpperInvariant()))
        {
            yield return Warning(field, "EPPO_INVALID",
                "Code OEPP invalide : 5 ou 6 caractères alphanumériques attendus (ex. TRZAW).");
        }
    }

    // 8. Stade BBCH
    private static IEnumerable<ValidationIssue> ValidateBbch(TreatmentRecord r)
    {
        const string field = nameof(TreatmentRecord.BbchStage);
        if (r.BbchStage is null)
        {
            if (r.ProductRequiresBbchStage && r.CropCategory == CropCategory.Regular)
            {
                yield return Error(field, "BBCH_REQUIRED",
                    "L'autorisation du produit limite l'usage à certains stades : le stade BBCH est obligatoire.");
            }
        }
        else if (r.BbchStage is < MinBbch or > MaxBbch)
        {
            yield return Error(field, "BBCH_OUT_OF_RANGE", "Le stade BBCH doit être compris entre 00 et 99.");
        }
    }
}
