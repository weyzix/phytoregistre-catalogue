using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Abstractions;

/// <summary>Valide une fiche selon les 8 rubriques du Règl. UE 2023/564.</summary>
public interface IRecordValidator
{
    /// <param name="today">Date du jour (injectée pour les tests).</param>
    IReadOnlyList<ValidationIssue> Validate(TreatmentRecord record, DateOnly today);
}

/// <summary>Délais légaux d'une fiche.</summary>
/// <param name="RegistrationDeadline">Enregistrement au plus tard 7 jours après application.</param>
/// <param name="ClientSheetDeadline">ETA : fiche au client max 30 jours après application (sinon null).</param>
/// <param name="ElectronicConversionDeadline">Conversion électronique (null en 2026 : pas d'obligation).</param>
public sealed record DeadlineInfo(
    DateOnly RegistrationDeadline,
    DateOnly? ClientSheetDeadline,
    DateOnly? ElectronicConversionDeadline);

public interface IDeadlineCalculator
{
    DeadlineInfo Compute(TreatmentRecord record);
}

/// <summary>Alertes transverses sur l'ensemble du registre.</summary>
/// <param name="RecordId">Fiche concernée, ou null pour une alerte globale (ex. stock).</param>
public sealed record RegisterAlert(Guid? RecordId, ValidationIssue Issue);

public interface IAlertService
{
    /// <summary>
    /// Délais dépassés/proches, fiche ETA non remise, x,y à remplacer par le n° PAC,
    /// stock négatif, rubriques manquantes.
    /// </summary>
    IReadOnlyList<RegisterAlert> GetAlerts(
        IReadOnlyList<TreatmentRecord> records,
        IReadOnlyList<Parcel> parcels,
        IReadOnlyList<StockBalance> stock,
        DateOnly today);
}

/// <summary>Référentiels embarqués (PPP, cultures OEPP, stades BBCH).</summary>
public interface IReferenceCatalog
{
    IReadOnlyList<Product> Products { get; }
    IReadOnlyList<Crop> Crops { get; }

    /// <summary>Principaux stades BBCH (code 0–99 → libellé), échelle générale.</summary>
    IReadOnlyList<BbchStage> BbchStages { get; }

    /// <summary>Lien vers la fiche du produit sur Fytoweb.</summary>
    Uri GetFytowebUri(Product product);
}

public sealed record BbchStage(int Code, string Description);

/// <summary>Solde de stock d'un produit.</summary>
public sealed record StockBalance(Guid ProductId, string ProductName, QuantityUnit Unit, decimal Quantity);

public interface IStockCalculator
{
    IReadOnlyList<StockBalance> ComputeBalances(
        IReadOnlyList<Product> products,
        IReadOnlyList<StockMovement> movements);

    /// <summary>Crée la sortie de stock correspondant à une fiche (quantité = −dose × surface).</summary>
    StockMovement CreateConsumption(TreatmentRecord record);
}

/// <summary>Conversion GPS (WGS84) ↔ Lambert belge 72 (EPSG:31370).</summary>
public interface ICoordinateConverter
{
    LambertPoint ToLambert72(GeoPoint wgs84);
    GeoPoint ToWgs84(LambertPoint lambert72);
}

/// <summary>Export du registre (Excel/CSV) et rapport de contrôle.</summary>
public interface IRegisterExporter
{
    /// <summary>CSV séparateur « ; », virgule décimale, UTF‑8 avec BOM (ouvrable dans Excel BE/FR).</summary>
    byte[] ExportCsv(IReadOnlyList<TreatmentRecord> records);

    /// <summary>Contenu tabulaire du rapport de contrôle (rendu en PDF par l'app).</summary>
    ControlReport BuildControlReport(
        IReadOnlyList<TreatmentRecord> records,
        string farmName,
        DateOnly from,
        DateOnly to);
}

public sealed record ControlReport(
    string Title,
    string Subtitle,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string Footer);

/// <summary>Sauvegarde / restauration complète.</summary>
public sealed record BackupSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<TreatmentRecord> Treatments { get; init; } = [];
    public IReadOnlyList<Parcel> Parcels { get; init; } = [];
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<Crop> Crops { get; init; } = [];
    public IReadOnlyList<StockMovement> StockMovements { get; init; } = [];
}

public interface IBackupSerializer
{
    string Serialize(BackupSnapshot snapshot);

    /// <exception cref="FormatException">JSON invalide ou version de schéma non supportée.</exception>
    BackupSnapshot Deserialize(string json);
}

/// <summary>Météo au moment de l'application — corrigeable ensuite.</summary>
public interface IWeatherService
{
    /// <returns>null si indisponible (hors ligne, erreur réseau) — ne lève pas d'exception réseau.</returns>
    Task<WeatherConditions?> GetConditionsAsync(GeoPoint location, DateTime localDateTime, CancellationToken ct = default);
}
