namespace PhytoRegistre.Core.Domain;

/// <summary>Produit phytopharmaceutique du référentiel (type Fytoweb).</summary>
public sealed partial record Product
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Dénomination commerciale complète.</summary>
    public required string CommercialName { get; init; }

    /// <summary>N° d'autorisation : nnnnP/B, P/P, G/B, G/P ou BS_nnnn.</summary>
    public required string AuthorizationNumber { get; init; }

    public QuantityUnit Unit { get; init; } = QuantityUnit.Liter;

    /// <summary>Produit SPe8 avec restriction horaire : heure de début obligatoire.</summary>
    public bool HasTimeRestriction { get; init; }

    /// <summary>Mention SPe8 « dangereux pour les abeilles ».</summary>
    public bool IsSpe8 { get; init; }

    /// <summary>L'autorisation limite l'usage à certains stades BBCH.</summary>
    public bool RequiresBbchStage { get; init; }

    /// <summary>Substances actives (texte libre), pour information.</summary>
    public string? ActiveSubstances { get; init; }

    /// <summary>Ajouté par l'utilisateur (pas issu du référentiel embarqué).</summary>
    public bool IsUserDefined { get; init; }

    /// <summary>
    /// Donnée d'exemple fictive (référentiel embarqué) : nom et n° d'autorisation à ne pas
    /// utiliser pour un traitement réel.
    /// </summary>
    public bool IsSample { get; init; }

    // --- Données issues de l'export officiel Fytoweb (null/vides pour un produit saisi à la main) ---

    /// <summary>Origine : "Fytoweb", "Exemple" ou "Utilisateur".</summary>
    public string? Source { get; init; }

    public ProductStatus Status { get; init; } = ProductStatus.Unknown;

    /// <summary>Fin de validité de l'autorisation (AUTH_dateTo).</summary>
    public DateOnly? AuthorizedUntil { get; init; }

    /// <summary>Utilisation tolérée jusqu'au (délai de grâce après retrait, useToleratedTo).</summary>
    public DateOnly? UseToleratedUntil { get; init; }

    /// <summary>Suspension en cours (suspensions.json) : début / fin éventuelle.</summary>
    public DateOnly? SuspendedFrom { get; init; }
    public DateOnly? SuspendedUntil { get; init; }

    /// <summary>Autorisé pour les utilisateurs professionnels (PRO ou AMA+PRO).</summary>
    public bool IsProfessional { get; init; } = true;

    public bool IsOrganicAllowed { get; init; }

    public string? FormulationType { get; init; }

    /// <summary>Usages autorisés par culture (doses, stades, délais).</summary>
    public IReadOnlyList<ProductUsage> Usages { get; init; } = [];
}

public enum ProductStatus
{
    Unknown = 0,
    Authorized = 1,
    Provisional = 2,
    Withdrawn = 3,
}

/// <summary>Un usage autorisé d'un produit sur une culture (export Fytoweb, pestUse.pestCrops[]).</summary>
public sealed partial record ProductUsage
{
    public required string CropNameFr { get; init; }
    public string? CropScientificName { get; init; }

    /// <summary>Ennemis visés (noms FR), pour information.</summary>
    public IReadOnlyList<string> TargetPests { get; init; } = [];

    public decimal? DoseMin { get; init; }
    public decimal? DoseMax { get; init; }

    /// <summary>Unité telle que Fytoweb la donne, ex. "l/ha", "kg/ha", "ml/m³".</summary>
    public string? DoseUnit { get; init; }

    /// <summary>Stade de croissance autorisé (texte, peut contenir "BBCH nn").</summary>
    public string? GrowthStage { get; init; }

    public int? MaxApplications { get; init; }

    /// <summary>Délai avant récolte (jours), si spécifié.</summary>
    public int? PreHarvestIntervalDays { get; init; }

    public string? Remarks { get; init; }
}

/// <summary>Culture du référentiel, avec son code OEPP (EPPO).</summary>
public sealed record Crop
{
    public required string Name { get; init; }

    /// <summary>Code OEPP, ex. TRZAW (froment d'hiver).</summary>
    public required string EppoCode { get; init; }

    public CropCategory Category { get; init; } = CropCategory.Regular;

    public bool IsUserDefined { get; init; }
}

/// <summary>Parcelle de l'exploitation (réutilisable d'une fiche à l'autre).</summary>
public sealed record Parcel
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    /// <summary>N° de parcelle PAC, si connu.</summary>
    public string? PacReference { get; init; }

    public string? CadastreNumber { get; init; }

    public string? Address { get; init; }

    /// <summary>Coordonnées Lambert belge 72 (EPSG:31370), en mètres.</summary>
    public LambertPoint? Coordinates { get; init; }

    public decimal? AreaHa { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Point en Lambert belge 72 (EPSG:31370), mètres.</summary>
public readonly record struct LambertPoint(double X, double Y);

/// <summary>Point WGS84 (GPS), degrés décimaux.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>Mouvement de stock d'un produit (entrée positive, sortie négative).</summary>
public sealed record StockMovement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ProductId { get; init; }
    public DateOnly Date { get; init; }
    /// <summary>Quantité signée, dans l'unité du produit.</summary>
    public decimal Quantity { get; init; }
    public StockMovementKind Kind { get; init; }
    /// <summary>Fiche de traitement à l'origine de la sortie, le cas échéant.</summary>
    public Guid? TreatmentId { get; init; }
    public string? Comment { get; init; }
}

/// <summary>Problème détecté sur une fiche (erreur bloquante, avertissement, info).</summary>
/// <param name="Field">Nom de la propriété concernée (ex. nameof(TreatmentRecord.DosePerHa)).</param>
/// <param name="Code">Code stable, ex. "PRODUCT_AUTH_INVALID".</param>
/// <param name="Message">Message en français pour l'utilisateur.</param>
public sealed record ValidationIssue(Severity Severity, string Field, string Code, string Message);
