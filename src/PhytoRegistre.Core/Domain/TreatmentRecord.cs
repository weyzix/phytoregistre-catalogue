namespace PhytoRegistre.Core.Domain;

/// <summary>Une fiche du registre d'utilisation des PPP (Règl. UE 2023/564).</summary>
public sealed record TreatmentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // 1. Type d'utilisation
    public UsageType UsageType { get; init; } = UsageType.Unspecified;

    // 2. Produit (copie figée au moment de l'enregistrement)
    public Guid? ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public string AuthorizationNumber { get; init; } = "";
    /// <summary>N° d'autorisation d'urgence (repris en commentaire).</summary>
    public string? EmergencyAuthorizationNumber { get; init; }
    public bool ProductHasTimeRestriction { get; init; }
    public bool ProductRequiresBbchStage { get; init; }

    // 3. Dose (quantité de produit par ha)
    public decimal DosePerHa { get; init; }
    public QuantityUnit DoseUnit { get; init; } = QuantityUnit.Liter;

    /// <summary>Unité du produit (copie de <see cref="Product.Unit"/>) ; null pour un produit saisi librement.</summary>
    public QuantityUnit? ProductUnit { get; init; }

    // 4. Surface réellement traitée (ha)
    public decimal TreatedAreaHa { get; init; }

    // 5. Localisation
    public TreatmentLocation Location { get; init; } = new();

    // 6. Moment
    public DateOnly ApplicationDate { get; init; }
    public TimeOnly? StartTime { get; init; }

    // 7. Culture / code OEPP
    public string? CropName { get; init; }
    public string? EppoCode { get; init; }
    public CropCategory CropCategory { get; init; } = CropCategory.Regular;

    // 8. Stade BBCH
    public int? BbchStage { get; init; }

    // Prestataire (ETA / trieur à façon)
    public ContractorInfo? Contractor { get; init; }

    // Traitement de semences / plants
    public SeedTreatmentInfo? Seed { get; init; }

    // Données complémentaires (lutte intégrée, cahiers des charges, ...)
    public string? Operator { get; init; }
    public string? Variety { get; init; }
    public string? TargetPest { get; init; }
    public string? PreviousCrop { get; init; }
    public string? DecisionBasis { get; init; }
    public WeatherConditions? Weather { get; init; }
    public string? Notes { get; init; }

    // Traçabilité
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Quantité totale de produit consommée = dose × surface.</summary>
    public decimal TotalQuantity => DosePerHa * TreatedAreaHa;
}

/// <summary>
/// 5. Localisation du traitement : copie figée au moment de l'enregistrement.
/// Priorité : n° PAC, sinon cadastre, adresse ou coordonnées x,y.
/// </summary>
public sealed record TreatmentLocation
{
    public Guid? ParcelId { get; init; }

    /// <summary>Nom usuel de la parcelle / serre / entrepôt.</summary>
    public string? Label { get; init; }

    public string? PacReference { get; init; }

    public string? CadastreNumber { get; init; }

    public string? Address { get; init; }

    public LambertPoint? Coordinates { get; init; }

    /// <summary>
    /// Référence permettant de localiser le traitement : n° PAC, cadastre, adresse ou x,y.
    /// Un simple nom usuel (<see cref="Label"/>) ne suffit pas.
    /// </summary>
    public bool HasLocatingReference =>
        !string.IsNullOrWhiteSpace(PacReference)
        || !string.IsNullOrWhiteSpace(CadastreNumber)
        || !string.IsNullOrWhiteSpace(Address)
        || Coordinates is not null;

    /// <summary>Au moins une information affichable (référence de localisation ou nom usuel).</summary>
    public bool HasAnyReference => HasLocatingReference || !string.IsNullOrWhiteSpace(Label);

    public static TreatmentLocation FromParcel(Parcel parcel) => new()
    {
        ParcelId = parcel.Id,
        Label = parcel.Name,
        PacReference = parcel.PacReference,
        CadastreNumber = parcel.CadastreNumber,
        Address = parcel.Address,
        Coordinates = parcel.Coordinates,
    };
}

/// <summary>Traitement réalisé par un tiers (ETA, trieur à façon).</summary>
public sealed record ContractorInfo
{
    /// <summary>Nom de l'agriculteur / client (obligatoire pour une ETA).</summary>
    public required string ClientName { get; init; }

    public string? ContractorName { get; init; }

    /// <summary>Date de remise de la fiche au client (max 30 j).</summary>
    public DateOnly? ClientSheetSentOn { get; init; }
}

/// <summary>Traitement de semences / plants.</summary>
public sealed record SeedTreatmentInfo
{
    public required string LotNumber { get; init; }

    /// <summary>Quantité de semences / plants traitée.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Unité de la quantité (kg, t, unités...).</summary>
    public string? QuantityUnit { get; init; }
}

/// <summary>Conditions météo lors de l'application (corrigeables).</summary>
public sealed record WeatherConditions
{
    public double? TemperatureC { get; init; }
    public double? WindSpeedKmh { get; init; }
    public double? RelativeHumidityPct { get; init; }
    public double? PrecipitationMm { get; init; }
    /// <summary>Source : "Open-Meteo", "Station", "Manuel".</summary>
    public string? Source { get; init; }
}
